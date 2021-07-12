﻿// Copyright © 2018 – Property of Tobii AB (publ) - All Rights Reserved

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tobii.StreamEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Tobii.XR
{
    public class StreamEngineTracker
    {
        private static readonly tobii_wearable_consumer_data_callback_t
            WearableDataCallback = OnWearableData; // Needed to prevent GC from removing callback

        private static readonly tobii_wearable_advanced_data_callback_t
            AdvancedWearableDataCallback = OnAdvancedWearableData; // Needed to prevent GC from removing callback

        private static readonly tobii_wearable_foveated_gaze_callback_t
            WearableFoveatedGazeCallback = OnWearableFoveatedGaze; // Needed to prevent GC from removing callback

        private readonly StreamEngineInteropWrapper _streamEngineInteropWrapper;
        private readonly tobii_custom_log_t _customLog;
        private Stopwatch _stopwatch = new Stopwatch();

        private bool _isReconnecting;
        private float _reconnectionTimestamp;
        private GCHandle _wearableDataCallbackPointer;
        private GCHandle _wearableAdvancedDataCallbackPointer;
        private GCHandle _wearableFoveatedDataCallbackPointer;
        private readonly List<JobHandle> _jobsDependingOnDevice = new List<JobHandle>();

        public StreamEngineContext Context { get; private set; }
        public tobii_feature_group_t LicenseLevel { get; private set; }
        public bool ConvergenceDistanceSupported { get; private set; }

        public StreamEngineTracker(StreamEngineTracker_Description description)
        {
            _streamEngineInteropWrapper = new StreamEngineInteropWrapper();
            _customLog = new tobii_custom_log_t {log_func = LogCallback};

            if (description == null)
            {
                description = new StreamEngineTracker_Description();
            }
            
            // Connect
            StreamEngineContext context;
            if (ConnectionHelper.TryConnect(_streamEngineInteropWrapper, description, out context, _customLog) == false)
            {
                throw new Exception("Failed to connect to tracker");
            }
            Context = context;

            // Subscribe to requested streams
            tobii_error_t result;
            if (description.WearableDataCallback != null)
            {
                _wearableDataCallbackPointer = GCHandle.Alloc(description.WearableDataCallback);
                result = Interop.tobii_wearable_consumer_data_subscribe(context.Device, WearableDataCallback, GCHandle.ToIntPtr(_wearableDataCallbackPointer));
                if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                {
                    throw new Exception("Failed to subscribe to eye tracking data: " + result);
                }
            }
            
            if (description.WearableAdvancedDataCallback != null)
            {
                _wearableAdvancedDataCallbackPointer = GCHandle.Alloc(description.WearableAdvancedDataCallback);
                result = Interop.tobii_wearable_advanced_data_subscribe(context.Device, AdvancedWearableDataCallback, GCHandle.ToIntPtr(_wearableAdvancedDataCallbackPointer));
                if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                {
                    throw new Exception("Failed to subscribe to eye tracking data: " + result);
                }
            }
            
            if (description.WearableFoveatedDataCallback != null)
            {
                _wearableFoveatedDataCallbackPointer = GCHandle.Alloc(description.WearableFoveatedDataCallback);
                result = Interop.tobii_wearable_foveated_gaze_subscribe(context.Device, WearableFoveatedGazeCallback, GCHandle.ToIntPtr(_wearableFoveatedDataCallbackPointer));
                if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                {
                    throw new Exception("Failed to subscribe to eye tracking data: " + result);
                }
            }

            // Get connection metadata
            CheckForCapabilities(Context.Device);
            tobii_feature_group_t licenseLevel;
            Interop.tobii_get_feature_group(Context.Device, out licenseLevel);
            LicenseLevel = licenseLevel;
        }

        public void Tick()
        {
            if (_isReconnecting)
            {
                // do not try to reconnect more than once every 500 ms
                if (Time.unscaledTime - _reconnectionTimestamp < 0.5f) return;

                var connected = ConnectionHelper.TryReconnect(_streamEngineInteropWrapper, Context.Device);
                _isReconnecting = !connected;
                return;
            }

            var result = ProcessCallback(Context.Device, _stopwatch);
            if (result == tobii_error_t.TOBII_ERROR_CONNECTION_FAILED)
            {
                UnityEngine.Debug.Log("Reconnecting...");
                _reconnectionTimestamp = Time.unscaledTime;
                _isReconnecting = true;
            }
        }

        public void Destroy()
        {
            if (_wearableDataCallbackPointer.IsAllocated) _wearableDataCallbackPointer.Free();
            if (_wearableAdvancedDataCallbackPointer.IsAllocated) _wearableAdvancedDataCallbackPointer.Free();
            if (_wearableFoveatedDataCallbackPointer.IsAllocated) _wearableFoveatedDataCallbackPointer.Free();
            if (Context == null) return;
            
            // Ensure all jobs having a pointer to this connection are completed
            foreach (var job in _jobsDependingOnDevice)
            {
                job.Complete();
            }
            var url = Context.Url;
            ConnectionHelper.Disconnect(_streamEngineInteropWrapper, Context);

            UnityEngine.Debug.Log(string.Format("Disconnected from {0}", url));

            _stopwatch = null;
        }

        private static tobii_error_t ProcessCallback(IntPtr deviceContext, Stopwatch stopwatch)
        {
            stopwatch.Reset();
            stopwatch.Start();
            var result = Interop.tobii_device_process_callbacks(deviceContext);
            stopwatch.Stop();
            var milliseconds = stopwatch.ElapsedMilliseconds; 

            if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                UnityEngine.Debug.LogError(string.Format("Failed to process callback. Error {0}", result));
            }

            if (milliseconds > 1)
            {
                UnityEngine.Debug.LogWarning(string.Format("Process callbacks took {0}ms", milliseconds));
            }

            return result;
        }

        private void CheckForCapabilities(IntPtr context)
        {
            bool supported;
            Interop.tobii_capability_supported(context,
                tobii_capability_t.TOBII_CAPABILITY_COMPOUND_STREAM_WEARABLE_CONVERGENCE_DISTANCE, out supported);
            ConvergenceDistanceSupported = supported;
        }
        
        #region Static callbacks
        
        [AOT.MonoPInvokeCallback(typeof(tobii_wearable_consumer_data_callback_t))]
        private static void OnWearableData(ref tobii_wearable_consumer_data_t data, IntPtr userData)
        {
            var gch = GCHandle.FromIntPtr(userData);
            var t = (WearableDataCallback) gch.Target;
            t.Invoke(ref data);
        }

        [AOT.MonoPInvokeCallback(typeof(tobii_wearable_advanced_data_callback_t))]
        private static void OnAdvancedWearableData(ref tobii_wearable_advanced_data_t data, IntPtr userData)
        {
            var gch = GCHandle.FromIntPtr(userData);
            var t = (WearableAdvancedDataCallback) gch.Target;
            t.Invoke(ref data);
        }
        
        [AOT.MonoPInvokeCallback(typeof(tobii_wearable_foveated_gaze_callback_t))]
        private static void OnWearableFoveatedGaze(ref tobii_wearable_foveated_gaze_t data, IntPtr userData)
        {
            var gch = GCHandle.FromIntPtr(userData);
            var t = (WearableFoveatedDataCallback) gch.Target;
            t.Invoke(ref data);
        }

        [AOT.MonoPInvokeCallback(typeof(Interop.tobii_log_func_t))]
        private static void LogCallback(IntPtr logContext, tobii_log_level_t level, string text)
        {
            UnityEngine.Debug.Log(text);
        }
        
        #endregion

        #region Timesync

        private NativeArray<tobii_error_t> _currentTimesyncResult;
        private NativeArray<tobii_timesync_data_t> _currentTimesyncData;
        private TimesyncJob _currentTimesyncJobData;
        private JobHandle? _currentTimesyncJobHandle;

        public JobHandle StartTimesyncJob()
        {
            if (_currentTimesyncJobHandle.HasValue)
            {
                Debug.LogError("Attempted to start a new timesync job before finishing the current.");
                return new JobHandle();
            }

            _currentTimesyncResult = new NativeArray<tobii_error_t>(1, Allocator.TempJob);
            _currentTimesyncData = new NativeArray<tobii_timesync_data_t>(1, Allocator.TempJob);
            _currentTimesyncJobData = new TimesyncJob
            {
                Device = Context.Device,
                Result = _currentTimesyncResult,
                TimesyncData = _currentTimesyncData
            };
            _currentTimesyncJobHandle = _currentTimesyncJobData.Schedule();
            _jobsDependingOnDevice.Add(_currentTimesyncJobHandle.Value);

            return _currentTimesyncJobHandle.Value;
        }

        public TobiiXR_AdvancedTimesyncData? FinishTimesyncJob()
        {
            if (_currentTimesyncJobHandle.HasValue)
            {
                _currentTimesyncJobHandle.Value.Complete();
                _jobsDependingOnDevice.RemoveAll(x => x.IsCompleted);
                _currentTimesyncJobHandle = null;

                TobiiXR_AdvancedTimesyncData? result;
                if (_currentTimesyncJobData.Result[0] == tobii_error_t.TOBII_ERROR_NO_ERROR)
                {
                    var d = _currentTimesyncJobData.TimesyncData[0];
                    result = new TobiiXR_AdvancedTimesyncData
                    {
                        StartSystemTimestamp = d.system_start_us,
                        EndSystemTimestamp = d.system_end_us,
                        DeviceTimestamp = d.tracker_us,
                    };
                }
                else
                {
                    result = null;
                    var message = Interop.tobii_error_message(_currentTimesyncJobData.Result[0]);
                    Debug.LogError("Error performing timesync: " + message);
                }

                // Free native arrays
                _currentTimesyncResult.Dispose();
                _currentTimesyncData.Dispose();
                return result;
            }
            else
            {
                Debug.LogWarning("Attempted to finish timesync job when no job was started.");
                return null;
            }
        }
        #endregion
    }
}

internal struct TimesyncJob : IJob
{
    [NativeDisableUnsafePtrRestriction] public IntPtr Device;
    public NativeArray<tobii_error_t> Result;
    public NativeArray<tobii_timesync_data_t> TimesyncData;

    public void Execute()
    {
        tobii_timesync_data_t timesyncData;
        Result[0] = Interop.tobii_timesync(Device, out timesyncData);
        TimesyncData[0] = timesyncData;
    }
}
