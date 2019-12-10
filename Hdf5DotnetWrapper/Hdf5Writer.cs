﻿using System;
using System.Linq;
using System.Reflection;

namespace Hdf5DotnetWrapper
{
    using hid_t = Int64;
    public partial class Hdf5
    {


        public static object WriteObject(hid_t groupId, object writeValue, string groupName = null)
        {
            if (writeValue == null)
            {
                throw new ArgumentNullException(nameof(writeValue));
            }
            Type tyObject = writeValue.GetType();
            Attribute attribute = Attribute.GetCustomAttributes(tyObject).SingleOrDefault(a => a is Hdf5GroupName);
            if (string.IsNullOrEmpty(groupName) && attribute != null)
            {
                groupName = ((Hdf5GroupName)attribute).Name;
            }
            bool createGroupName = !string.IsNullOrWhiteSpace(groupName);
            if (createGroupName)
                groupId = CreateGroup(groupId, groupName);


            foreach (Attribute attr in Attribute.GetCustomAttributes(tyObject))
            {
                if (attr is Hdf5SaveAttribute legAt)
                {
                    Hdf5Save kind = legAt.SaveKind;
                    if (kind == Hdf5Save.DoNotSave)
                        return writeValue;
                }
            }

            WriteProperties(tyObject, writeValue, groupId);
            WriteFields(tyObject, writeValue, groupId);
            WriteHdf5Attributes(tyObject, groupId, groupName);
            if (createGroupName)
                CloseGroup(groupId);
            return (writeValue);
        }

        private static void WriteHdf5Attributes(Type type, hid_t groupId, string name, string datasetName = null)
        {
            foreach (Attribute attr in Attribute.GetCustomAttributes(type))
            {
                if (attr is Hdf5Attribute)
                {
                    var h5at = attr as Hdf5Attribute;
                    WriteAttribute(groupId, name, h5at.Name, datasetName);
                }
                if (attr is Hdf5Attributes)
                {
                    var h5ats = attr as Hdf5Attributes;
                    WriteAttributes<string>(groupId, name, h5ats.Names, datasetName);
                }
            }
        }

        private static void WriteFields(Type tyObject, object writeValue, hid_t groupId)
        {
            FieldInfo[] miMembers = tyObject.GetFields(BindingFlags.DeclaredOnly |
       /*BindingFlags.NonPublic |*/ BindingFlags.Instance | BindingFlags.Public);

            foreach (FieldInfo info in miMembers)
            {
                if (NoSavePresent(Attribute.GetCustomAttributes(info))) continue;
                object infoVal = info.GetValue(writeValue);
                if (infoVal == null)
                    continue;
                string name = info.Name;
                //bool isEnumerable = info.FieldType.GetInterface(typeof(IEnumerable<>).FullName) != null;
                WriteField(infoVal, info, groupId, name);
            }
        }

        private static void WriteProperties(Type tyObject, object writeValue, hid_t groupId)
        {
            PropertyInfo[] miMembers = tyObject.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            foreach (PropertyInfo info in miMembers)
            {
                if (NoSavePresent(Attribute.GetCustomAttributes(info))) continue;
                object infoVal = info.GetValue(writeValue, null);
                if (infoVal == null)
                    continue;
                string name = info.Name;

                WriteField(infoVal, info, groupId, name);
            }
        }

        private static bool NoSavePresent(Attribute[] attributes)
        {
            bool noSaveAttr = false;
            foreach (Attribute attr in attributes)
            {
                var legAttr = attr as Hdf5SaveAttribute;
                var kind = legAttr?.SaveKind;
                if (kind == Hdf5Save.DoNotSave)
                {
                    noSaveAttr = true;
                    break;
                }
            }

            return noSaveAttr;
        }

        private static void WriteField(object infoVal, FieldInfo filedInfo, hid_t groupId, string name)
        {
            Type ty = infoVal.GetType();
            TypeCode code = Type.GetTypeCode(ty);

            if (ty.IsArray)
            {
                var elType = ty.GetElementType();
                TypeCode elCode = Type.GetTypeCode(elType);
                if (elCode != TypeCode.Object || ty == typeof(TimeSpan[]))
                    dsetRW.WriteArray(groupId, name, (Array)infoVal);
                else
                {
                    CallByReflection<(int, hid_t)>(nameof(WriteCompounds), elType, new[] { groupId, name, infoVal });
                }
            }
            else if (primitiveTypes.Contains(code) || ty == typeof(TimeSpan))
            //WriteOneValue(groupId, name, infoVal);
            {
                (int success, hid_t CreatedgroupId) = CallByReflection<(int, hid_t)>(nameof(WriteOneValue), ty, new[] { groupId, name, infoVal });
                //todo: fix it
                //add its attributes if there are: 
                //foreach (Attribute attr in Attribute.GetCustomAttributes(filedInfo))
                //{
                //    if (attr is Hdf5Attribute)
                //    {
                //        var h5at = attr as Hdf5Attribute;
                //        WriteStringAttribute(groupId, name, h5at.Name, name);
                //    }

                //    if (attr is Hdf5Attributes)
                //    {
                //        var h5ats = attr as Hdf5Attributes;
                //        WriteAttributes<string>(groupId, name, h5ats.Names, attr.);
                //    }
                //}
            }
            else
                WriteObject(groupId, infoVal, name);
        }
        private static void WriteField(object infoVal, PropertyInfo propertyInfo, hid_t groupId, string name)
        {
            Type ty = infoVal.GetType();
            TypeCode code = Type.GetTypeCode(ty);

            if (ty.IsArray)
            {
                var elType = ty.GetElementType();
                TypeCode elCode = Type.GetTypeCode(elType);
                if (elCode != TypeCode.Object || ty == typeof(TimeSpan[]))
                    dsetRW.WriteArray(groupId, name, (Array)infoVal);
                else
                {
                    {
                        CallByReflection<int>(nameof(WriteCompounds), elType, new[] { groupId, name, infoVal });
                        //add its attributes
                    }
                }
            }
            else if (primitiveTypes.Contains(code) || ty == typeof(TimeSpan))
                CallByReflection<(int success, hid_t CreatedgroupId)>(nameof(WriteOneValue), ty, new[] { groupId, name, infoVal });
            else
                WriteObject(groupId, infoVal, name);
        }
        static T CallByReflection<T>(string name, Type typeArg, object[] values)
        {
            // Just for simplicity, assume it's public etc
            MethodInfo method = typeof(Hdf5).GetMethod(name);
            MethodInfo generic = method.MakeGenericMethod(typeArg);
            return (T)generic.Invoke(null, values);

        }

    }
}
