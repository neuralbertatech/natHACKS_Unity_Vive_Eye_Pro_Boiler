// Copyright © 2018 – Property of Tobii AB (publ) - All Rights Reserved

using System;
using System.Collections.Generic;
using System.Globalization;
using Tobii.StreamEngine;
using Unity.Jobs;
using UnityEngine;

namespace Tobii.XR
{
    /// <summary>
    /// Uses Tobii's Stream Engine library to provide eye tracking data to TobiiXR
    /// </summary>
    [ProviderDisplayName("Tobii")]
    public class TobiiProvider : IEyeTrackingProvider
    {
        private const int AdvancedDataQueueSize = 30;
        private readonly TobiiXR_EyeTrackingData _eyeTrackingDataLocal = new TobiiXR_EyeTrackingData();

        private readonly TobiiXR_AdvancedEyeTrackingData _advancedEyeTrackingData =
            new TobiiXR_AdvancedEyeTrackingData();

        private readonly Queue<TobiiXR_AdvancedEyeTrackingData> _advancedData =
            new Queue<TobiiXR_AdvancedEyeTrackingData>(AdvancedDataQueueSize);

        private Vector3 _foveatedGazeDirectionLocal;
        private StreamEngineTracker _streamEngineTracker;
        private HmdToWorldTransformer _hmdToWorldTransformer;
        private Matrix4x4 _localToWorldMatrix;

        public Matrix4x4 LocalToWorldMatrix
        {
            get { return _localToWorldMatrix; }
        }

        public TobiiXR_EyeTrackingData EyeTrackingDataLocal
        {
            get { return _eyeTrackingDataLocal; }
        }

        public Vector3 FoveatedGazeDirectionLocal
        {
            get { return _foveatedGazeDirectionLocal; }
        }

        public bool HasValidOcumenLicense
        {
            get { return _streamEngineTracker.LicenseLevel >= tobii_feature_group_t.TOBII_FEATURE_GROUP_PROFESSIONAL; }
        }

        public TobiiXR_AdvancedEyeTrackingData AdvancedEyeTrackingData
        {
            get { return _advancedEyeTrackingData; }
        }

        public Queue<TobiiXR_AdvancedEyeTrackingData> AdvancedData
        {
            get { return _advancedData; }
        }

        public StreamEngineContext InternalHandle
        {
            get { return _streamEngineTracker.Context; }
        }

        public TobiiXR_EyeTrackerMetadata GetMetadata()
        {
            tobii_device_info_t info;
            Interop.tobii_get_device_info(_streamEngineTracker.Context.Device, out info);
            float outputFrequency;
            Interop.tobii_get_output_frequency(_streamEngineTracker.Context.Device, out outputFrequency);
            var result = new TobiiXR_EyeTrackerMetadata
            {
                SerialNumber = info.serial_number,
                Model = info.model,
                RuntimeVersion = info.runtime_build_version,
                OutputFrequency = outputFrequency > 1 ? outputFrequency.ToString(CultureInfo.InvariantCulture) : "Unknown",
            };
            return result;
        }

        public bool Initialize()
        {
            return Initialize(new StreamEngineTracker_Description
            {
                WearableDataCallback = OnWearableData,
                // WearableFoveatedDataCallback = OnFoveatedData, // This is not supported by devkit/ttp
            });
        }

        public bool InitializeAdvanced(string licenseKey)
        {
            return Initialize(new StreamEngineTracker_Description
            {
                License = new[] {licenseKey},
                WearableAdvancedDataCallback = OnAdvancedWearableData,
            });
        }

        private bool Initialize(StreamEngineTracker_Description description)
        {
            try
            {
                return Initialize(new StreamEngineTracker(description));
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                return false;
            }
        }

        private bool Initialize(StreamEngineTracker streamEngineTracker)
        {
            _streamEngineTracker = streamEngineTracker;
            _hmdToWorldTransformer = new HmdToWorldTransformer(estimatedEyeTrackerLatency_s: 0.012f);
            return true;
        }

        public void Tick()
        {
            _streamEngineTracker.Tick();
            _hmdToWorldTransformer.Tick();
            _localToWorldMatrix = _hmdToWorldTransformer.GetLocalToWorldMatrix();
        }

        public void Destroy()
        {
            if (_streamEngineTracker != null)
            {
                _streamEngineTracker.Destroy();
                _streamEngineTracker = null;
            }
        }

        private void OnWearableData(ref tobii_wearable_consumer_data_t data)
        {
            StreamEngineDataMapper.FromConsumerData(_eyeTrackingDataLocal, ref data,
                _streamEngineTracker.ConvergenceDistanceSupported, CoordinatesHelper.GetHeadToCenterEyeTranslation());
            _eyeTrackingDataLocal.Timestamp = Time.unscaledTime;
        }

        private void OnAdvancedWearableData(ref tobii_wearable_advanced_data_t data)
        {
            var advancedData = _advancedData.Count >= AdvancedDataQueueSize
                ? _advancedData.Dequeue()
                : new TobiiXR_AdvancedEyeTrackingData();
            StreamEngineDataMapper.MapAdvancedData(advancedData, ref data,
                _streamEngineTracker.ConvergenceDistanceSupported, CoordinatesHelper.GetHeadToCenterEyeTranslation());
            _advancedData.Enqueue(advancedData);

            // Keep a copy of latest received value
            EyeTrackingDataHelper.Copy(advancedData, _advancedEyeTrackingData);

            // Also fill in consumer api
            StreamEngineDataMapper.FromAdvancedData(_eyeTrackingDataLocal, ref data,
                _streamEngineTracker.ConvergenceDistanceSupported, CoordinatesHelper.GetHeadToCenterEyeTranslation());
            _eyeTrackingDataLocal.Timestamp = Time.unscaledTime;
        }

        private void OnFoveatedData(ref tobii_wearable_foveated_gaze_t data)
        {
            _foveatedGazeDirectionLocal.x =
                data.gaze_direction_combined_normalized_xyz.x * -1; // Tobii to Unity CS conversion
            _foveatedGazeDirectionLocal.y = data.gaze_direction_combined_normalized_xyz.y;
            _foveatedGazeDirectionLocal.z = data.gaze_direction_combined_normalized_xyz.z;
        }

        #region Timesync

        public JobHandle StartTimesyncJob()
        {
            return _streamEngineTracker.StartTimesyncJob();
        }

        public TobiiXR_AdvancedTimesyncData? FinishTimesyncJob()
        {
            return _streamEngineTracker.FinishTimesyncJob();
        }

        public long GetSystemTimestamp()
        {
            long timestamp;
            Interop.tobii_system_clock(_streamEngineTracker.Context.Api, out timestamp);
            return timestamp;
        }

        #endregion
    }
}
