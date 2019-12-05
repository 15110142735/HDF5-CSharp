﻿using System.Collections.Generic;
using Hdf5DotnetWrapper.DataTypes;
using Hdf5DotnetWrapper.Interfaces;

namespace Hdf5DotnetWrapper
{

    [Hdf5Save(Hdf5Save.Save)]
    public class Hdf5AcquisitionFile : IHdf5AcquisitionFile
    {
        public Hdf5Patient Patient { get; set; }
        public Hdf5Recording Recording { get; set; }
        public Hdf5Channel[] Channels { get; set; }
        public Hdf5Events Events { get; set; }
        [Hdf5Save(Hdf5Save.DoNotSave)]
        public List<Hdf5Event> EventList { get; private set; }

        [Hdf5Save(Hdf5Save.DoNotSave)]
        public short[,] Data { get; set; }

        public Hdf5AcquisitionFile()
        {
            Patient = new Hdf5Patient();
            Recording = new Hdf5Recording();
            EventList = new List<Hdf5Event>();
            Events = new Hdf5Events();

            Recording.PropertyChanged += (sender, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(Hdf5Recording.NrOfChannels))
                    Channels = new Hdf5Channel[Recording.NrOfChannels];
            };

        }


        public void EventListToEvents()
        {
            Events = new Hdf5Events(EventList.Count);
            for (int i = 0; i < EventList.Count; i++)
            {
                Events.Times[i] = EventList[i].Time;
                Events.Durations[i] = EventList[i].Duration;
                Events.Events[i] = EventList[i].Event;
            }

        }

    }











}
