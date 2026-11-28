using System;
using System.Collections;
using UnityEngine;

namespace RP0
{
    public class AirlaunchUtils
    {

        public static IEnumerator DoAirlaunchRoutine(AirlaunchParams launchParams, Guid vesselId, bool skipCountdown = false)
        {
            do
            {
                yield return new WaitForFixedUpdate();

                if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.id != vesselId)
                {
                    ScreenMessages.PostScreenMessage("Airlaunch cancelled", 5f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);
                    yield break;
                }
            }
            while (FlightGlobals.ActiveVessel.vesselSpawning);

            // Make sure that the vessel situation transitions from Prelaunch to Landed before airlaunching
            if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
                FlightGlobals.ActiveVessel.situation = Vessel.Situations.LANDED;

            KCTUtilities.DoAirlaunch(launchParams);

            SpaceCenterManagement.Instance.StartCoroutine(ClobberToPrelaunch());

            if (skipCountdown)
                yield break;

            // Arm both hold mechanisms immediately before the snapshot yield.
            // HoldVesselUnpack blocks the GoOffRails gate (FixedUpdate-based counter,
            // immune to fps variance). SetPhysicsHoldExpiryOverride blocks any direct GoOffRails
            // call (render-frame-based counter). DoAirlaunch's HoldVesselUnpack(60) only covers
            // ~1 second; we maintain our own hold throughout the 10-second countdown.
            OrbitPhysicsManager.HoldVesselUnpack(2);
            FlightGlobals.ActiveVessel.SetPhysicsHoldExpiryOverride(60);

            // Wait one fixed update for the teleport to settle before recording initial state.
            yield return new WaitForFixedUpdate();

            // Since we're keeping physics hold lock active artificially, KSP won't release the control lock in the usual way either.
            InputLockManager.RemoveControlLock("physicsHold");

            // The load screen overlay will be kept active until either the vessel comes off rails or 400 frames have passed.
            // Since it ain't coming off the rails any time soon, better to just kill it manually.
            LoadingBufferMask.Instance.Hide();
            LoadingBufferMask.Instance.StopCoroutine("ShowDuration");

            if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.id != vesselId)
                yield break;

            // Snapshot the orbital state we want to hold constant: a straight-line trajectory
            // at constant velocity. Without this, patched-conic propagation curves the velocity
            // vector downward and changes speed over the 10-second hold.
            double holdStartUT = Planetarium.GetUniversalTime();
            // Position relative to the reference body in Unity (XYZ) space.
            Vector3d holdStartRelPos = FlightGlobals.ActiveVessel.GetWorldPos3D() - FlightGlobals.ActiveVessel.mainBody.position;
            Vector3d up = holdStartRelPos.normalized;
            Vector3d omega = FlightGlobals.ActiveVessel.mainBody.angularVelocity;
            // Compute the surface-relative launch velocity directly from the airlaunch parameters,
            // using the same convention as HyperEdit_Utilities.DoAirlaunch: east = ω × r, north = east × up.
            Vector3d planetRotVelocity = Vector3d.Cross(omega, holdStartRelPos);
            Vector3d east = planetRotVelocity.normalized;
            Vector3d north = Vector3d.Cross(east, up);
            Vector3d holdStartSurfVel = (Math.Sin(Mathf.Deg2Rad * launchParams.LaunchAzimuth) * east +
                                         Math.Cos(Mathf.Deg2Rad * launchParams.LaunchAzimuth) * north) * launchParams.Velocity;

            // Hold physics inactive for 10 seconds by continuously refreshing the physics hold
            // expiry override (prevents GoOffRails from unpacking) and patching the orbital state
            // each fixed update to counteract Keplerian drift.
            for (int i = 10; i > 0; i--)
            {
                ScreenMessages.PostScreenMessage($"Airlaunching in {i}...", 1f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);

                float elapsed = 0f;
                while (elapsed < 1f)
                {
                    Vessel v = FlightGlobals.ActiveVessel;
                    if (v == null || v.id != vesselId)
                    {
                        RP0Debug.LogWarning($"Cancelling airlaunch routine, vessel id changed: {vesselId} -> {v.id}");
                        yield break;
                    }

                    // Advance the position at the airlaunch surface velocity, co-rotated with the planet.
                    // The vessel drifts along the launch heading at launchParams.Velocity, matching
                    // how far a carrier aircraft would travel during the countdown.
                    double deltaT = Planetarium.GetUniversalTime() - holdStartUT;
                    float rotAngleDeg = (float)(omega.magnitude * deltaT * Mathf.Rad2Deg);
                    Quaternion rot = Quaternion.AngleAxis(rotAngleDeg, (Vector3)omega.normalized);
                    Vector3d velocityOffset = holdStartSurfVel - planetRotVelocity;
                    Vector3d newRelPos = (Vector3d)(rot * (Vector3)(holdStartRelPos + velocityOffset * deltaT));

                    // Inertial velocity = frame velocity at new position + rotated surface launch velocity.
                    Vector3d newObtVel = Vector3d.Cross(omega, newRelPos) + (Vector3d)(rot * (Vector3)holdStartSurfVel);

                    // UpdateFromStateVectors expects Zup (orbital) space, i.e. the .xzy swizzle of Unity space.
                    v.orbit.UpdateFromStateVectors(newRelPos.xzy, newObtVel.xzy, v.mainBody, Planetarium.GetUniversalTime());

                    // Also move the vessel transform to match, mirroring what OrbitDriver.updateFromParameters does.
                    // Without this the transform stays at the Keplerian position set by
                    // the preceding FixedUpdate, and our orbit update only takes effect next frame.
                    Vector3d localCom = (QuaternionD)v.orbitDriver.driverTransform.rotation * (Vector3d)v.localCoM;
                    v.SetPosition(v.mainBody.position + newRelPos - localCom);

                    // Refresh both hold mechanisms before yielding. HoldVesselUnpack(2) covers up to 2 consecutive FixedUpdates.
                    // SetPhysicsHoldExpiryOverride(60) covers direct GoOffRails call sites.
                    OrbitPhysicsManager.HoldVesselUnpack(2);
                    v.SetPhysicsHoldExpiryOverride(60);

                    yield return new WaitForFixedUpdate();
                    elapsed += Time.fixedDeltaTime;
                }
            }

            OrbitPhysicsManager.HoldVesselUnpack(1);
            FlightGlobals.ActiveVessel?.SetPhysicsHoldExpiryOverride(1);
        }

        /// <summary>
        /// Need to keep the vessel in Prelaunch state for a while if Principia is installed.
        /// Otherwise the vessel will spin out in a random way.
        /// Without Principia, Prelaunch state prevents RO's persistent rotation feature from enabling itself.
        /// </summary>
        /// <returns></returns>
        private static IEnumerator ClobberToPrelaunch()
        {
            if (FlightGlobals.ActiveVessel == null)
                yield return null;

            do
            {
                FlightGlobals.ActiveVessel.situation = Vessel.Situations.PRELAUNCH;
                yield return new WaitForFixedUpdate();
            } while (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.packed);

            // Need to fire this so trip logger etc notice we're flying now.
            RP0Debug.Log($"Finished clobbering vessel situation of {FlightGlobals.ActiveVessel.name} to PRELAUNCH, now firing change event to FLYING.");
            FlightGlobals.ActiveVessel.situation = Vessel.Situations.FLYING;
            GameEvents.onVesselSituationChange.Fire(new GameEvents.HostedFromToAction<Vessel, Vessel.Situations>(FlightGlobals.ActiveVessel, Vessel.Situations.PRELAUNCH, Vessel.Situations.FLYING));
        }
    }
}
