using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KerbalConstructionTime
{
    public static class TypeExtensions
    {
        public static T GetPublicStaticValue<T>(this Type type, string name) where T : class
        {
            return (T)Utilities.GetMemberInfoValue(type.GetMember(name, BindingFlags.Public | BindingFlags.Static).FirstOrDefault(), null);
        }

        public static T GetPublicValue<T>(this Type type, string name, object instance) where T : class
        {
            return (T)Utilities.GetMemberInfoValue(type.GetMember(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).FirstOrDefault(), instance);
        }

        public static T GetPrivateMemberValue<T>(this Type type, string name, object instance, int index = -1)
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            object value = Utilities.GetMemberInfoValue(type.GetMember(name, flags).FirstOrDefault(), instance);
            if (value != null)
            {
                return (T)value;
            }
            
            KCTDebug.Log($"Could not get value by name '{name}', getting by index '{index}'");
            if (index >= 0)
            {
                List<MemberInfo> members = type.GetMembers(flags).Where(m => m.ToString().Contains(typeof(T).ToString())).ToList();
                if (members.Count > index)
                {
                    return (T)Utilities.GetMemberInfoValue(members[index], instance);
                }
            }
            throw new Exception($"No members of type '{typeof(T)}' found for name '{name}' at index '{index}' for type '{type}'");
        }

        public static object GetPrivateMemberValue(this Type type, string name, object instance, int index = -1)
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            object value = Utilities.GetMemberInfoValue(type.GetMember(name, flags).FirstOrDefault(), instance);
            if (value != null)
            {
                return value;
            }

            KCTDebug.Log($"Could not get value by name '{name}', getting by index '{index}'");
            if (index >= 0)
            {
                List<MemberInfo> members = type.GetMembers(flags).ToList();
                if (members.Count > index)
                {
                    return Utilities.GetMemberInfoValue(members[index], instance);
                }
            }
            throw new Exception($"No members found for name '{name}' at index '{index}' for type '{type}'");
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
