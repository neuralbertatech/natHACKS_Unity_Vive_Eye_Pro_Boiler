using System;
using Tobii.StreamEngine;
using UnityEngine;

namespace Tobii.XR
{
    public class StreamEngineTracker_Description
    {
        public string[] License = new string[0];
        public string SupportedIntegrationType = "hmd";
        public WearableDataCallback WearableDataCallback;
        public WearableAdvancedDataCallback WearableAdvancedDataCallback;
        public WearableFoveatedDataCallback WearableFoveatedDataCallback;
    }
    
    public delegate void WearableDataCallback(ref tobii_wearable_consumer_data_t data);
    public delegate void WearableAdvancedDataCallback(ref tobii_wearable_advanced_data_t data);
    public delegate void WearableFoveatedDataCallback(ref tobii_wearable_foveated_gaze_t data);
}