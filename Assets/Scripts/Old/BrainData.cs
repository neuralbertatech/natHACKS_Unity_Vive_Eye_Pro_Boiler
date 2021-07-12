using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Accord;
using Accord.Math;
using brainflow;

public class BrainData : MonoBehaviour
{
    private BoardShim board_shim = null;
    private BrainFlowInputParams input_params = null;
    private MLModel concentration = null;
    private static int board_id = 0;
    private int sampling_rate = 0;
    private int[] eeg_channels = null;
    private int[] accelChannels = null;


    // Start is called before the first frame update
    void Start()
    {
        try
        {
            input_params = new BrainFlowInputParams();
            
            input_params.serial_port = "COM3";
            board_id = (int)BoardIds.CYTON_DAISY_BOARD;

            BoardShim.set_log_file("brainflow_log.txt");
            BoardShim.enable_dev_board_logger();

            board_shim = new BoardShim(board_id, input_params);
            board_shim.prepare_session();
            board_shim.start_stream(450000, "file://brainflow_data.csv:w");
            BrainFlowModelParams concentration_params = new BrainFlowModelParams((int)BrainFlowMetrics.CONCENTRATION, (int)BrainFlowClassifiers.REGRESSION);
            concentration = new MLModel(concentration_params);
            concentration.prepare();

            sampling_rate = BoardShim.get_sampling_rate(board_id);
            eeg_channels = BoardShim.get_eeg_channels(board_id);
            accelChannels = BoardShim.get_accel_channels(board_id);
            Debug.Log("Brainflow streaming was started");
        }
        catch (BrainFlowException e)
        {
            Debug.Log(e);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if ((board_shim == null) || (concentration == null))
        {
            return;
        }
        int number_of_data_points = sampling_rate * 4; // 4 second window is recommended for concentration and relaxation calculations
        Debug.Log(number_of_data_points);
        double[,] data = board_shim.get_current_board_data(number_of_data_points);
        if (data.GetRow(0).Length < number_of_data_points)
        {
            // wait for more data
            return;
        }
        // prepare feature vector
        Tuple<double[], double[]> bands = DataFilter.get_avg_band_powers (data, eeg_channels, sampling_rate, true);
        double[] feature_vector = bands.Item1.Concatenate (bands.Item2);
        // calc and print concetration level
        // for synthetic board this value should be close to 1, because of sin waves ampls and freqs
        Debug.Log("Concentration: " + concentration.predict (feature_vector));
    }

    private void OnDestroy()
    {
        if (board_shim != null)
        {
            try
            {
                board_shim.release_session();
                concentration.release();
            }
            catch (BrainFlowException e)
            {
                Debug.Log(e);
            }
            Debug.Log("Brainflow streaming was stopped");
        }
    }
}