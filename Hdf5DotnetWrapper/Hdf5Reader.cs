﻿using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HDF.PInvoke;

namespace Hdf5DotnetWrapper
{

    using hid_t = Int64;

    public partial class Hdf5
    {

        public static T ReadObject<T>(hid_t groupId, T readValue, string groupName)
        {
            if (readValue == null)
            {
                throw new ArgumentNullException(nameof(readValue));
            }

            Type tyObject = readValue.GetType();
            foreach (Attribute attr in Attribute.GetCustomAttributes(tyObject))
            {
                if (attr is Hdf5GroupName)
                    groupName = (attr as Hdf5GroupName).Name;
                if (attr is Hdf5SaveAttribute)
                {
                    Hdf5SaveAttribute atLeg = attr as Hdf5SaveAttribute;
                    if (atLeg.SaveKind == Hdf5Save.DoNotSave)
                        return readValue;
                }
            }
            bool isGroupName = !string.IsNullOrWhiteSpace(groupName);
            if (isGroupName)
                groupId = H5G.open(groupId, Hdf5Utils.NormalizedName(groupName));

            ReadFields(tyObject, readValue, groupId);
            ReadProperties(tyObject, readValue, groupId);

            if (isGroupName)
                CloseGroup(groupId);
            return readValue;
        }

        public static T ReadObject<T>(hid_t groupId, string groupName) where T : new()
        {
            T readValue = new T();
            return ReadObject(groupId, readValue, groupName);
        }

        private static void ReadFields(Type tyObject, object readValue, hid_t groupId)
        {
            FieldInfo[] miMembers = tyObject.GetFields(BindingFlags.DeclaredOnly |
       /*BindingFlags.NonPublic |*/ BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo info in miMembers)
            {
                bool nextInfo = false;
                string alternativeName = string.Empty;
                foreach (Attribute attr in Attribute.GetCustomAttributes(info))
                {
                    if (attr is Hdf5EntryNameAttribute nameAttribute)
                    {
                        alternativeName = nameAttribute.Name;
                    }

                    if (attr is Hdf5SaveAttribute attribute)
                    {
                        Hdf5Save kind = attribute.SaveKind;
                        nextInfo = (kind == Hdf5Save.DoNotSave);
                    }
                    else
                        nextInfo = false;
                }
                if (nextInfo) continue;

                Type ty = info.FieldType;
                TypeCode code = Type.GetTypeCode(ty);

                string name = info.Name;
                Trace.WriteLine($"groupname: {tyObject.Name}; field name: {name}");

                if (ty.IsArray)
                {
                    var elType = ty.GetElementType();
                    TypeCode elCode = Type.GetTypeCode(elType);

                    Array values;
                    if (elCode != TypeCode.Object)
                    {
                        values = dsetRW.ReadArray(elType, groupId, name, alternativeName);
                    }
                    else
                    {
                        values = CallByReflection<Array>(nameof(ReadCompounds), elType, new object[] { groupId, name });
                    }
                    info.SetValue(readValue, values);
                }
                else if (primitiveTypes.Contains(code) || ty == typeof(TimeSpan))
                {
                    Array values = dsetRW.ReadArray(ty, groupId, name, alternativeName);
                    // get first value depending on rank of the matrix
                    int[] first = new int[values.Rank].Select(f => 0).ToArray();
                    info.SetValue(readValue, values.GetValue(first));
                }
                else
                {
                    Object value = info.GetValue(readValue);
                    if (value != null)
                        ReadObject(groupId, value, name);
                }
            }
        }

        private static void ReadProperties(Type tyObject, object readValue, hid_t groupId)
        {
            PropertyInfo[] miMembers = tyObject.GetProperties(/*BindingFlags.DeclaredOnly |*/
       BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo info in miMembers)
            {
                bool nextInfo = false;
                string alternativeName = string.Empty;
                foreach (Attribute attr in Attribute.GetCustomAttributes(info))
                {
                    if (attr is Hdf5SaveAttribute hdf5SaveAttribute)
                    {
                        Hdf5Save kind = hdf5SaveAttribute.SaveKind;
                        nextInfo = (kind == Hdf5Save.DoNotSave);
                    }
                    if (attr is Hdf5EntryNameAttribute hdf5EntryNameAttribute)
                    {
                        alternativeName = hdf5EntryNameAttribute.Name;
                    }
                }
                if (nextInfo) continue;
                Type ty = info.PropertyType;
                TypeCode code = Type.GetTypeCode(ty);
                string name = info.Name;
                

                if (ty.IsArray)
                {
                    var elType = ty.GetElementType();
                    TypeCode elCode = Type.GetTypeCode(elType);

                    Array values;
                    if (elCode != TypeCode.Object || ty == typeof(TimeSpan[]))
                    {
                        values = dsetRW.ReadArray(elType, groupId, name, alternativeName);
                    }
                    else
                    {
                        var obj = CallByReflection<IEnumerable>(nameof(ReadCompounds), elType, new object[] { groupId, name });
                        var objArr = (obj).Cast<object>().ToArray();
                        values = Array.CreateInstance(elType, objArr.Length);
                        Array.Copy(objArr, values, objArr.Length);
                    }
                    info.SetValue(readValue, values);
                }
                else if (primitiveTypes.Contains(code) || ty == typeof(TimeSpan))
                {
                    Array values = dsetRW.ReadArray(ty, groupId, name, alternativeName);
                    int[] first = new int[values.Rank].Select(f => 0).ToArray();
                    info.SetValue(readValue, values.GetValue(first));
                }
                else
                {
                    Object value = info.GetValue(readValue, null);
                    if (value != null)
                    {
                        value = ReadObject(groupId, value, name);
                        info.SetValue(readValue, value);
                    }
                }
            }
        }


    }

}
