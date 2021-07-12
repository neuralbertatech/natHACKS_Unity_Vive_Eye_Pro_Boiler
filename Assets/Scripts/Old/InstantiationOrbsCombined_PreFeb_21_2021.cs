using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Accord;
using Accord.Math;
using brainflow;

// using HighlightAtGaze;

public class InstantiationOrbsCombined_PreFeb_21_2021 : MonoBehaviour
{
    
    // ----------------------------------------------- //
    // Visible elements
    // ----------------------------------------------- //

    public GameObject brain;
    public GameObject orb;
    // public GameObject face;
    // public GameObject target;

    public float orb_size; // 3
    public float orb_distance; // 3.5
    public float head_size; // 1
    public float speed; // 2

    float close;
    float far; 
    float ratio;

    public int simulation; // if 1 simulates brain data, if 0 from board
    private double[] filtered;

    // [HideInInspector] 
    // public List<GameObject> orbList = new List<GameObject>();
    // [HideInInspector] 
    // public List<GameObject> orbtargetList = new List<GameObject>();

    private List<GameObject> orbList = new List<GameObject>();
    private List<GameObject> c_orbtargetList = new List<GameObject>();
    private List<GameObject> f_orbtargetList = new List<GameObject>();

    private int orb_num = 16;
    private int[] orb_states = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    private int frame_count = 0;

    double[,] list = new double[16,3] {
    {-0.06734486,  0.04071033, -0.01094572}, // F7
    {-0.04815716,  0.05090548,  0.04043975}, // F3
    {0.04968343, 0.0520495 , 0.03911898}, // F4
    {0.0700096 ,  0.04257685, -0.01150164}, // F8
    {-0.06264376, -0.01114863,  0.06168519}, // C3
    {0.06433047, -0.01044761,  0.0609395}, // C4
    {-0.06942608, -0.07040219, -0.00238371}, // P7
    {-0.05080589, -0.07551572,  0.05361679}, // P3
    {0.05335484, -0.07529757,  0.054212}, // P4
    {0.07002167, -0.07003375, -0.00243451}, // P8
    {-0.02819185, -0.10777896,  0.00847191}, // O1
    {0.02860323, -0.10749813,  0.00843453}, // O2
    {-0.06942608, -0.07040219, -0.00238371}, // T7
    {0.07002167, -0.07003375, -0.00243451}, // T8
    {-0.02821418,  0.080432  , -0.0066997}, // Fp1
    {0.02863169,  0.08137015, -0.00678597}, // Fp2
    // {-0.08250133, -0.04312126, -0.06516252}, // M1
    // {0.08223085, -0.04314005, -0.06520565}, // M2
    };

    // ----------------------------------------------- //
    // Brain data elements
    // ----------------------------------------------- //

    private BoardShim board_shim = null;
    private BrainFlowInputParams input_params = null;
    private MLModel concentration = null;
    private static int board_id = 0;
    private int sampling_rate = 0;
    private int[] eeg_channels = null;
    private int[] accelChannels = null;

    // ----------------------------------------------- //
    // Sounds elements
    // ----------------------------------------------- //

    public float frequency1 = 200;
     public float frequency2 = 100;

    public float sampleRate = 44100;
    public float waveLengthInSeconds = 2.0f;
 
    AudioSource audioSource;
    int timeIndex = 0;

    private List<AudioSource> audioList = new List<AudioSource>();
    private int[] audio_states = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    public void Start()
    {
        // ----------------------------------------------- //
        // Visible elements
        // ----------------------------------------------- //

        // instantiate each head orb via the list coordinates
        var highlightscript = Type.GetType( "HighlightAtGaze" );

        for (int x = 0; x < orb_num; ++x)
        {
            GameObject reference = Instantiate(orb, new UnityEngine.Vector3((float)(list[x,0]),(float)(list[x,1]),(float)(list[x,2])), Quaternion.identity);
            // Debug.Log(reference.transform.position);
            reference.transform.position = RotateAroundOrigin(reference.transform.position,-90);
            reference.transform.localScale *= orb_size;
            reference.transform.position *= orb_distance*2;
            // if (x==14 | x==15) {
            //    reference.GetComponent<Renderer>().material.color = new Color(1,1,200); // Frontal
            // }
            // if (x==10 | x==11) {
            //    reference.GetComponent<Renderer>().material.SetColor("_Color", Color.green);
            // }
            reference.GetComponent<Renderer>().material.SetColor("_Color", Color.green);
            // Add eye position based color change
            Tobii.XR.Examples.HighlightAtGaze highlight = reference.AddComponent<Tobii.XR.Examples.HighlightAtGaze>() as Tobii.XR.Examples.HighlightAtGaze;
            // Add an audioSource 
            audioSource = reference.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0; //force 2D sound
            audioSource.Stop(); //avoids audiosource from starting to play automatically

            // Add 
            orbList.Add(reference);
            audioList.Add(audioSource);
        }    

        // Create and position the brain
        GameObject ref_brain = Instantiate(brain, new UnityEngine.Vector3(0.0f,0.0f,0.0f), Quaternion.identity);
        ref_brain.transform.Rotate(34.3f,58.6f,-14.2f);
        ref_brain.transform.localScale *= head_size;
        ref_brain.transform.position += new UnityEngine.Vector3(0.0f, -1.13f, 0.0f);
    
        // Create and position the face + set transparency 
        // GameObject ref_face = Instantiate(face, new UnityEngine.Vector3(0.0f,0.0f,0.0f), Quaternion.identity);

        // // Define each of the target's positions
        // for (int i = 0; i < orb_num; ++i)
        // {
        //     GameObject target = new GameObject();
        //     // target.transform.localScale = new UnityEngine.Vector3(0.1f, 1f, 0.1f);
        //     // target.transform.position = new UnityEngine.Vector3(0.0f, 0.0f, 0.0f);
        //     target.transform.position = new UnityEngine.Vector3((float)(list[i,0]),(float)(list[i,1]),(float)(list[i,2]));
        //     target.transform.position = RotateAroundOrigin(target.transform.position,-90);
        //     target.transform.position *= orb_distance*1.0f;

        //     orbtargetList.Add(target);
        // }

        // Define each of the close target's positions
        for (int i = 0; i < orb_num; ++i)
        {
            GameObject c_target = new GameObject();
            // target.transform.localScale = new UnityEngine.Vector3(0.1f, 1f, 0.1f);
            // target.transform.position = new UnityEngine.Vector3(0.0f, 0.0f, 0.0f);
            c_target.transform.position = new UnityEngine.Vector3((float)(list[i,0]),(float)(list[i,1]),(float)(list[i,2]));
            c_target.transform.position = RotateAroundOrigin(c_target.transform.position,-90);
            c_target.transform.position *= orb_distance*1.0f;

            c_orbtargetList.Add(c_target);
        }

        // Define each of the far target's positions
        for (int i = 0; i < orb_num; ++i)
        {
            GameObject f_target = new GameObject();
            // target.transform.localScale = new UnityEngine.Vector3(0.1f, 1f, 0.1f);
            // target.transform.position = new UnityEngine.Vector3(0.0f, 0.0f, 0.0f);
            f_target.transform.position = new UnityEngine.Vector3((float)(list[i,0]),(float)(list[i,1]),(float)(list[i,2]));
            f_target.transform.position = RotateAroundOrigin(f_target.transform.position,-90);
            f_target.transform.position *= orb_distance*2.5f;

            f_orbtargetList.Add(f_target);
        }

        // Create and position the floor.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.position = new UnityEngine.Vector3(0.0f, -10.5f, 0.0f);
        floor.transform.localScale = new UnityEngine.Vector3(25.0f, 1.0f, 25.0f);
    
        // ----------------------------------------------- //
        // Brain data elements
        // ----------------------------------------------- //

        if (simulation == 1)
        {
            try     
            {
                input_params = new BrainFlowInputParams();

                BoardShim.set_log_file("brainflow_log.txt");
                BoardShim.enable_dev_board_logger();

                board_id = (int)BoardIds.SYNTHETIC_BOARD;
                board_shim = new BoardShim(board_id, input_params);
                board_shim.prepare_session();
                board_shim.start_stream(450000, "file://brainflow_data.csv:w");
                BrainFlowModelParams concentration_params = new BrainFlowModelParams((int)BrainFlowMetrics.CONCENTRATION, (int)BrainFlowClassifiers.REGRESSION);
                concentration = new MLModel(concentration_params);
                concentration.prepare();

                sampling_rate = BoardShim.get_sampling_rate(board_id);
                eeg_channels = BoardShim.get_eeg_channels(board_id);
                Debug.Log("Brainflow streaming was started");
            }
            catch (BrainFlowException e)
            {
                Debug.Log(e);
            }
        }
        else if (simulation == 0)
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

        
    }

    // Update is called once per frame
    void Update()
    {
        
        // ----------------------------------------------- //
        // Brain data elements
        // ----------------------------------------------- //

        if ((board_shim == null) || (concentration == null))
        {
            return;
        }
        int i = 0;
        int number_of_data_points = sampling_rate * 4; // 4 second window is recommended for concentration and relaxation calculations
        
        double[,] unprocessed_data = board_shim.get_current_board_data(number_of_data_points);
        if (unprocessed_data.GetRow(0).Length < number_of_data_points)
        {
            // wait for more data
            return;
        }

        Debug.Log("// -------------------------------------- //");
        for (i = 0; i < eeg_channels.Length; i++)
        {
            // filtered = DataFilter.perform_rolling_filter (unprocessed_data.GetRow (eeg_channels[i]), 3, (int)AggOperations.MEAN);

            filtered = DataFilter.perform_wavelet_denoising (unprocessed_data.GetRow (eeg_channels[i]), "db4", 3);

            Debug.Log("channel " + eeg_channels[i] + " = " + filtered[i].ToString());
        }

        // prepare feature vector
        Tuple<double[], double[]> bands = DataFilter.get_avg_band_powers (unprocessed_data, eeg_channels, sampling_rate, true);
        
        double[] feature_vector = bands.Item1.Concatenate (bands.Item2);
        // calc and print concetration level
        // for synthetic board this value should be close to 1, because of sin waves ampls and freqs
        Debug.Log("Concentration: " + concentration.predict (feature_vector));
        

        // ----------------------------------------------- //
        // Visible elements
        // ----------------------------------------------- //
        
        i = 0;

        // iterate through each point and move each orb
        
        if (frame_count < 500)
        {
            foreach (var point in orbList) {
                float step = speed * Time.deltaTime;
                // transform.position = UnityEngine.Vector3.MoveTowards(transform.position, target.position, step);
                // check the current state of the orb target 
                // if 0 it is moving towards origin
                // otherwise it is moving away from origin
                // away from origin target is 2 times the size of each respective orb's instantiation point
                
                GameObject c_target = c_orbtargetList[i];
                GameObject f_target = f_orbtargetList[i];


                if (orb_states[i] == 0) 
                {
                    point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, c_target.transform.position, step);
                    if (UnityEngine.Vector3.Distance(point.transform.position, c_target.transform.position) < 0.1f)
                    {
                        orb_states[i] = 1;
                    }
                } 
                if (orb_states[i] == 1)
                {
                    point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, f_target.transform.position, step);
                    if (UnityEngine.Vector3.Distance(point.transform.position, f_target.transform.position) < 0.1f)
                    {
                        orb_states[i] = 0;

                    }
                }
                i += 1;
            }
            frame_count += 1;
        }
        else
        {
            foreach (var point in orbList) 
            {
                float step = speed * Time.deltaTime;

                GameObject c_target = c_orbtargetList[i];
                GameObject f_target = f_orbtargetList[i];

                close = UnityEngine.Vector3.Distance(point.transform.position, c_target.transform.position);
                far = UnityEngine.Vector3.Distance(point.transform.position, f_target.transform.position);
                ratio = close / far;
                Debug.Log("Distance to close target: " + close);
                Debug.Log("Distance to far target: " + far);
                Debug.Log("Ratio of close to far: " + ratio);

                if (ratio < filtered[i]/10)
                {
                    point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, f_target.transform.position, step);
                    // point.GetComponent<Renderer>().material.SetColor("_Color", Color.blue);

                }
                else
                {
                    point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, c_target.transform.position, step); 
                    // point.GetComponent<Renderer>().material.SetColor("_Color", Color.yellow);
                }

                // // if orb state is 0 (currently moving towards center target)
                // // then if the ratio of close to far is less than filtered/10
                // // then start moving towards the far target
                // if (orb_states[i] == 0)
                // {
                //     if (ratio < filtered[i]/10)
                //     {
                //         orb_states[i] = 1;
                //         point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, f_target.transform.position, step);
                //     }
                //     else
                //     {
                //        point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, c_target.transform.position, step); 
                //     }
                // }
                // // if orb state is 1 (currently moving towards far target)
                // // then if the ratio of close to far is greater than filtered/10
                // // then start moving towards the center target
                // else if (orb_states[i] == 1)
                // {
                    
                //     if (ratio < filtered[i]/10)
                //     {
                //         orb_states[i] = 0;
                //         point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, c_target.transform.position, step); 
                //     }
                //     else
                //     {
                //         point.transform.position = UnityEngine.Vector3.MoveTowards(point.transform.position, f_target.transform.position, step);
                //     }
                i += 1;
            }
        }
        
        // ----------------------------------------------- //
        // Audio elements
        // ----------------------------------------------- //



    }

    // ----------------------------------------------- //
    // Visible functions
    // ----------------------------------------------- //

    UnityEngine.Vector3 RotateAroundOrigin(UnityEngine.Vector3 startPos,float angle)
    {
        startPos.Normalize();
        Quaternion rot = Quaternion.Euler(angle, 0.0f, 0.0f); // Rotate [angle] degrees about the x axis.
        startPos = rot * startPos;
        return startPos;
    }   

    UnityEngine.Vector3 ResizeHead(UnityEngine.Vector3 coord, int size)
    {
        coord = size * coord;
        return coord;
    }

    // ----------------------------------------------- //
    // Brain data functions
    // ----------------------------------------------- //

    void OnDestroy()
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

    void OnAudioFilterRead(float[] data, int channels)
    {
        for(int i = 0; i < data.Length; i+= channels)
        {          
            data[i] = CreateSine(timeIndex, frequency1, sampleRate);
           
            if(channels == 2)
                data[i+1] = CreateSine(timeIndex, frequency2, sampleRate);
           
            timeIndex++;
           
            //if timeIndex gets too big, reset it to 0
            if(timeIndex >= (sampleRate * waveLengthInSeconds))
            {
                timeIndex = 0;
            }
        }
    }

    public float CreateSine(int timeIndex, float frequency, float sampleRate)
    {
        return Mathf.Sin(2 * Mathf.PI * timeIndex * frequency / sampleRate);
    }

    // ----------------------------------------------- //
    // Audio functions
    // ----------------------------------------------- //
}

