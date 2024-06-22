﻿#define CIBUILD_disabled
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("RP0")]
[assembly: AssemblyDescription("Plugin for RP-0 mod")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("KSP-RO")]
[assembly: AssemblyProduct("")]
[assembly: AssemblyCopyright("Copyright © KSP-RO 2014-2021 CC-BY-NC-SA 4.0")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("36d2b381-e48b-4938-8919-15973b32ce5a")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
#if CIBUILD
[assembly: AssemblyFileVersion("@MAJOR@.@MINOR@.@PATCH@.@BUILD@")]
[assembly: AssemblyInformationalVersion("@ASSEMBLYINFORMATIONALVERSION@")]
[assembly: KSPAssembly("RP-0", @MAJOR@, @MINOR@)]
#else
[assembly: AssemblyFileVersion("3.0.0.0")]
[assembly: AssemblyInformationalVersion("3.0.0.0")]
[assembly: KSPAssembly("RP-0", 3, 0)]
#endif

[assembly: KSPAssemblyDependency("ModularFlightIntegrator", 1, 0)]
[assembly: KSPAssemblyDependency("RealFuels", 15, 7)]
[assembly: KSPAssemblyDependency("ROUtils", 1, 0, 1)]
[assembly: KSPAssemblyDependency("ClickThroughBlocker", 1, 8)]
[assembly: KSPAssemblyDependency("ContractConfigurator", 2, 6)]
[assembly: KSPAssemblyDependency("ToolbarController", 1, 0)]
[assembly: KSPAssemblyDependency("KSPCommunityFixes", 1, 22, 1)]
