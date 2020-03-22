﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Hdf5DotnetWrapper.Interfaces
{
    public interface IHdf5ReaderWriter
    {
        (int success, long CreatedgroupId) WriteFromArray<T>(long groupId, string name, Array dset, string datasetName = null);
        (bool success, Array result) ReadToArray<T>(long groupId, string name, string alternativeName);
        (int success, long CreatedgroupId) WriteStrings(long groupId, string name, IEnumerable<string> collection, string datasetName = null);
        (bool success, IEnumerable<string>) ReadStrings(long groupId, string name, string alternativeName);
    }
}
