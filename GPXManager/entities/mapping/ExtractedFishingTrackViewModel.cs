﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MapWinGIS;
using System.Xml;

namespace GPXManager.entities.mapping
{
    public class ExtractedFishingTrackViewModel
    {
        private int _counter;
        private int _idCounter;
        public delegate void TrackExtractedFromSource(ExtractedFishingTrackViewModel s, ExtractTrackEventArgs e);
        public event TrackExtractedFromSource TrackExtractedFromSourceCreated;

        private bool _editSuccess;
        public ObservableCollection<ExtractedFishingTrack> ExtractedFishingTrackCollection { get; set; }
        private ExtractedFishingTrackRepository ExtractedFishingTracks { get; set; }

        public async Task<List<ExtractedFishingTrack>> ExtractTracksFromSourcesAsync(bool save = false, bool makeShapefile = false)
        {
            return await Task.Run(() => ExtractTracksFromSources(save, makeShapefile));
        }
        public int Count()
        {
            return ExtractedFishingTrackCollection.Count;
        }

        public List<ExtractedFishingTrack> GetTracks(ExtractedTrackSourceType sourceType, int sourceID)
        {
            var list = ExtractedFishingTrackCollection
                .Where(t => t.TrackSourceType == sourceType && t.TrackSourceID == sourceID).ToList();

            return ExtractedFishingTrackCollection
                .Where(t => t.TrackSourceType == sourceType && t.TrackSourceID == sourceID).ToList();
        }

        public List<ExtractedFishingTrack> AllExtractedFishingTracks { get; private set; }
        private List<ExtractedFishingTrack> CreateFromSource(CTXFile ctx = null, DeviceGPX gpx = null, bool save = false)
        {
            FishingTripAndGearRetrievalTracks result = null;
            List<ExtractedFishingTrack> listOfExtractedTracks = new List<ExtractedFishingTrack>();
            if (ctx == null && gpx == null)
                throw new ArgumentNullException("Error: Source is not specified");

            ExtractedTrackSourceType sourceType = ExtractedTrackSourceType.TrackSourceTypeNone;
            var deviceName = "";
            int id = 0;
            if (ctx != null)
            {
                sourceType = ExtractedTrackSourceType.TrackSourceTypeCTX;
                id = ctx.RowID;
                deviceName = Entities.CTXFileViewModel.GetFile(id, false).UserName;
            }
            else if (gpx != null)
            {
                sourceType = ExtractedTrackSourceType.TrackSourceTypeGPX;
                id = gpx.RowID;
                deviceName = Entities.DeviceGPXViewModel.GetDeviceGPX(id).GPS.DeviceName;
            }

            bool procced = false;
            var extractedTracks = Entities.ExtractedFishingTrackViewModel.GetTracks(sourceType, id);
            if (extractedTracks != null && extractedTracks.Count > 0)
            {
                result = new FishingTripAndGearRetrievalTracks { TripShapefile = null };
                foreach (var item in extractedTracks)
                {
                    item.FromDatabase = true;
                    var shp = new Shape();
                    if (shp.Create(ShpfileType.SHP_POLYLINE))
                    {
                        shp.CreateFromString(item.SerializedTrack);
                        item.SegmentSimplified = shp;
                        item.TrackOriginal = null;
                        DetectedTrack dt = new DetectedTrack { Shape = shp, ExtractedFishingTrack = item, Length = item.LengthOriginal, Accept = true };
                        if (result.GearRetrievalTracks == null)
                        {
                            result.GearRetrievalTracks = new List<DetectedTrack>();
                        }
                        //if (MapWindowManager.BSCBoundaryShapefile == null || !shp.Crosses(MapWindowManager.BSCBoundaryShapefile.Shape[0]))
                        //{
                            result.GearRetrievalTracks.Add(dt);
                        //}
                    }
                }
                procced = result.GearRetrievalTracks != null;
            }
            else
            {

                if (ctx != null)
                {
                    if (ctx.XML.Length == 0)
                    {
                        ctx.XML = Entities.CTXFileViewModel.GetXMLOfCTX(ctx);
                        if (ctx.XML.Length == 0)
                        {
                            return null;
                        }
                    }


                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(ctx.XML);
                    var tracknodes = doc.SelectNodes("//T");
                    if (ctx.TrackingInterval == null)
                    {
                        ctx.TrackingInterval = Entities.CTXFileViewModel.GetGPSTimerIntervalFromCTX(ctx, true);
                    }



                    result = ShapefileFactory.GearRetrievalTrackShapeFromCTX(tracknodes, ctx.TrackingInterval);


                    //procced = result != null && result.GearRetrievalTracks.Where(T => T.Accept).ToList().Count > 0;


                }
                else
                {
                    var gpxFile = Entities.GPXFileViewModel.ConvertToGPXFile(gpx);

                    if (gpxFile.GPSTimerInterval == null)
                    {
                        gpxFile.GPSTimerInterval = Entities.DeviceGPXViewModel.GetDeviceGPX(id).TimerInterval;
                    }
                    result = ShapefileFactory.GearRetrievalTrackShapeFromGPX(gpxFile, gpxFile.GPSTimerInterval);
                    //procced = result != null && result.GearRetrievalTracks.Where(T => T.Accept).ToList().Count > 0;

                }

                procced = result != null && result.GearRetrievalTracks.Where(T => T.Accept).ToList().Count > 0;
            }



            if (procced)
            {
                foreach (var item in result.GearRetrievalTracks.Where(t => t.Accept))
                {
                    ExtractedFishingTrack extractedTrack = new ExtractedFishingTrack
                    {
                        DateAdded = DateTime.Now,
                        TrackSourceType = sourceType,
                        TrackSourceID = id,
                        Start = item.ExtractedFishingTrack.Start,
                        End = item.ExtractedFishingTrack.End,
                        LengthOriginal = item.ExtractedFishingTrack.LengthOriginal,
                        LengthSimplified = item.ExtractedFishingTrack.LengthSimplified,
                        TrackPointCountOriginal = item.ExtractedFishingTrack.TrackPointCountOriginal,
                        TrackPointCountSimplified = item.ExtractedFishingTrack.TrackPointCountSimplified,
                        AverageSpeed = item.ExtractedFishingTrack.AverageSpeed,
                        TrackOriginal = item.ExtractedFishingTrack.TrackOriginal,
                        SegmentSimplified = item.ExtractedFishingTrack.SegmentSimplified,
                        SerializedTrack = item.ExtractedFishingTrack.SegmentSimplified.SerializeToString(),
                        DeviceName = deviceName,
                        FromDatabase = item.ExtractedFishingTrack.FromDatabase,
                        SerializedTrackUTM = item.ExtractedFishingTrack.SerializedTrackUTM
                    };
                    if (!save)
                    {

                        extractedTrack.ID = ++_idCounter;
                    }
                    listOfExtractedTracks.Add(extractedTrack);
                }
            }
            //ExtractTrackResult etr = new ExtractTrackResult { Success = result != null, ExtractedTracks = listOfExtractedTracks, SourceType = sourceType, SourceID = id };
            return listOfExtractedTracks;
        }

        private async Task<List<ExtractedFishingTrack>> ExtractTracksFromSources(bool save = false, bool makeShapefile = false)
        {
            _counter = 0;
            var list = new List<ExtractedFishingTrack>();
            var allCTXFiles = await Entities.CTXFileViewModel.GetAllAsync(false);
            foreach (CTXFile cf in allCTXFiles)
            {
                var list1 = CombineExtractedTracks(CreateFromSource(ctx: cf, save: save));
                if (list1 != null && list1.Count > 0)
                {
                    list.AddRange(list1);
                }
                //list.AddRange(CombineExtractedTracks(CreateFromSource(ctx: cf, save: save)));
            }

            foreach (DeviceGPX gpx in Entities.DeviceGPXViewModel.GetAllDeviceWaypointGPX())
            {
                if (gpx.GPXType == "track")
                {
                    var list2 = CombineExtractedTracks(CreateFromSource(gpx: gpx, save: save));
                    if (list2 != null && list2.Count > 0)
                    {
                        list.AddRange(list2);
                    }

                }

            }

            if (save)
            {
                int saveCount = 0;
                foreach (var item in list)
                {
                    if (!item.FromDatabase)
                    {
                        item.ID = NextRecordNumber;
                        if (AddRecordToRepo(item))
                        {
                            saveCount++;
                            if (TrackExtractedFromSourceCreated != null)
                            {
                                ExtractTrackEventArgs e = new ExtractTrackEventArgs
                                {
                                    Context = "Saved track",
                                    Counter = saveCount
                                };
                                TrackExtractedFromSourceCreated(this, e);
                            }
                        }
                    }
                }
            }

            if (makeShapefile)
            {
                ExtractedFishingTracksSF = new Shapefile();
                if (ExtractedFishingTracksSF.CreateNewWithShapeID("", ShpfileType.SHP_POLYLINE))
                {
                    ExtractedFishingTracksSF.GeoProjection = globalMapping.GeoProjection;
                    ExtractedFishingTracksSF.Key = "extracted_tracks";
                    ExtractedFishingTracksSF.EditAddField("ID", FieldType.INTEGER_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("DeviceName", FieldType.STRING_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("DateAdded", FieldType.DATE_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("SourceType", FieldType.STRING_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("SourceID", FieldType.INTEGER_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("Start", FieldType.DATE_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("End", FieldType.DATE_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("LenOriginal", FieldType.DOUBLE_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("LenSimplified", FieldType.DOUBLE_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("TrckPtsOriginal", FieldType.INTEGER_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("TrckPtsSimplified", FieldType.INTEGER_FIELD, 1, 1);
                    ExtractedFishingTracksSF.EditAddField("AvgSpeed", FieldType.DOUBLE_FIELD, 1, 1);

                    foreach (var item in list)
                    {
                        var idx = ExtractedFishingTracksSF.EditAddShape(item.SegmentSimplified);
                        if (idx >= 0)
                        {
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["ID"], idx, item.ID);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["DateAdded"], idx, item.DateAdded);
                            if (item.TrackSourceType == ExtractedTrackSourceType.TrackSourceTypeCTX)
                            {
                                ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["SourceType"], idx, "CTX");
                            }
                            else
                            {
                                ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["SourceType"], idx, "GPX");
                            }
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["SourceID"], idx, item.TrackSourceID);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["DeviceName"], idx, item.DeviceName);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["Start"], idx, item.Start);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["End"], idx, item.End);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["LenOriginal"], idx, item.LengthOriginal);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["LenSimplified"], idx, item.LengthSimplified);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["TrckPtsOriginal"], idx, item.TrackPointCountOriginal);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["TrckPtsSimplified"], idx, item.TrackPointCountSimplified);
                            ExtractedFishingTracksSF.EditCellValue(ExtractedFishingTracksSF.FieldIndexByName["AvgSpeed"], idx, item.AverageSpeed);
                        }
                    }
                }
            }
            AllExtractedFishingTracks = list;
            return list;
        }

        private List<ExtractedFishingTrack> CombineExtractedTracks(List<ExtractedFishingTrack> efts)
        {
            var list = new List<ExtractedFishingTrack>();
            if (efts != null)
            {
                foreach (var item in efts)
                {
                    list.Add(item);
                    if (TrackExtractedFromSourceCreated != null)
                    {
                        ExtractTrackEventArgs e = new ExtractTrackEventArgs
                        {
                            Context = "Extracted track",
                            ExtractedFishingTrack = item,
                            Counter = ++_counter
                        };
                        TrackExtractedFromSourceCreated(this, e);
                    }
                }
                return list;
            }
            return null;
        }



        public Shapefile ExtractedFishingTracksSF { get; private set; }
        public ExtractedFishingTrackViewModel()
        {
            ExtractedFishingTracks = new ExtractedFishingTrackRepository();
            ExtractedFishingTrackCollection = new ObservableCollection<ExtractedFishingTrack>(ExtractedFishingTracks.ExtractedFishingTracks);
            ExtractedFishingTrackCollection.CollectionChanged += ExtractedFishingTrackCollection_CollectionChanged;
        }

        public ExtractedFishingTrack CurrentEntity { get; private set; }
        private void ExtractedFishingTrackCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _editSuccess = false;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        int newIndex = e.NewStartingIndex;
                        ExtractedFishingTrack newItem = ExtractedFishingTrackCollection[newIndex];
                        if (ExtractedFishingTracks.Add(newItem))
                        {
                            CurrentEntity = newItem;
                            _editSuccess = true;
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    {
                        List<ExtractedFishingTrack> tempListOfRemovedItems = e.OldItems.OfType<ExtractedFishingTrack>().ToList();
                        _editSuccess = ExtractedFishingTracks.Delete(tempListOfRemovedItems[0].ID);
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    {
                        List<ExtractedFishingTrack> tempList = e.NewItems.OfType<ExtractedFishingTrack>().ToList();
                        _editSuccess = ExtractedFishingTracks.Update(tempList[0]);      // As the IDs are unique, only one row will be effected hence first index only
                    }
                    break;
            }
        }

        public int NextRecordNumber
        {
            get
            {
                if (ExtractedFishingTrackCollection.Count == 0)
                {
                    return 1;
                }
                else
                {
                    return ExtractedFishingTracks.MaxRecordNumber() + 1;
                }
            }
        }

        public List<ExtractedFishingTrack> GetAll()
        {
            return ExtractedFishingTrackCollection.OrderBy(t => t.ID).ToList();
        }
        public bool AddRecordToRepo(ExtractedFishingTrack eft)
        {
            if (eft == null)
                throw new ArgumentNullException("Error: The argument is Null");

            ExtractedFishingTrackCollection.Add(eft);
            return _editSuccess;
        }

        public bool UpdateRecordInRepo(ExtractedFishingTrack eft)
        {
            if (eft == null)
                throw new Exception("Error: The argument is Null");

            int index = 0;
            while (index < ExtractedFishingTrackCollection.Count)
            {
                if (ExtractedFishingTrackCollection[index].ID == eft.ID)
                {
                    ExtractedFishingTrackCollection[index] = eft;
                    break;
                }
                index++;
            }
            return _editSuccess;
        }

        public bool DeleteRecordFromRepo(ExtractedFishingTrack eft)
        {
            if (eft == null)
                throw new Exception("Error: The argument is Null");

            int index = 0;
            while (index < ExtractedFishingTrackCollection.Count)
            {
                if (ExtractedFishingTrackCollection[index].ID == eft.ID)
                {
                    ExtractedFishingTrackCollection.RemoveAt(index);
                    break;
                }
                index++;
            }

            return _editSuccess;
        }
    }
}