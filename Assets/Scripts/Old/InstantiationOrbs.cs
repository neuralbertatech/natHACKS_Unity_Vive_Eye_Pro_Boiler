using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstantiationOrbs : MonoBehaviour
{
    public GameObject brain;
    public GameObject orb;
    // public GameObject face;
    // public GameObject target;


    public float orb_size; // 3
    public float orb_distance; // 3.5
    public float head_size; // 1
    public float speed; // 2

    [HideInInspector] 
    public List<GameObject> orbList = new List<GameObject>();
    [HideInInspector] 
    public List<GameObject> orbtargetList = new List<GameObject>();

    private int orb_num = 18;

    private Vector3 temp_ftarg;

    private int[] orb_states = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    double[,] list = new double[18,3] {
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
    {-0.08250133, -0.04312126, -0.06516252}, // M1
    {0.08223085, -0.04314005, -0.06520565}, // M2
    };

    public void Start()
    {
        

        // instantiate each head orb via the list coordinates
        for (int x = 0; x < orb_num; ++x)
        {
            GameObject reference = Instantiate(orb, new Vector3((float)(list[x,0]),(float)(list[x,1]),(float)(list[x,2])), Quaternion.identity);
            // Debug.Log(reference.transform.position);
            reference.transform.position = RotateAroundOrigin(reference.transform.position,-90);
            reference.transform.localScale *= orb_size;
            reference.transform.position *= orb_distance*2;
            if (x==14 | x==15) {
                reference.GetComponent<Renderer>().material.color = new Color(1,1,200); // Frontal
            }
            if (x==10 | x==11) {
                reference.GetComponent<Renderer>().material.color = new Color(1,200,1); // Occipital
            }
            orbList.Add(reference);
        }    

        // Create and position the brain
        GameObject ref_brain = Instantiate(brain, new Vector3(0.0f,0.0f,0.0f), Quaternion.identity);
        ref_brain.transform.Rotate(34.3f,58.6f,-14.2f);
        ref_brain.transform.localScale *= head_size;
        ref_brain.transform.position += new Vector3(0.0f, -1.13f, 0.0f);
    
        // Create and position the face + set transparency 
        // GameObject ref_face = Instantiate(face, new Vector3(0.0f,0.0f,0.0f), Quaternion.identity);

        // Define each of the target's positions
        for (int i = 0; i < orb_num; ++i)
        {
            GameObject target = new GameObject();
            // target.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
            // target.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
            target.transform.position = new Vector3((float)(list[i,0]),(float)(list[i,1]),(float)(list[i,2]));
            target.transform.position = RotateAroundOrigin(target.transform.position,-90);
            target.transform.position *= orb_distance*1.0f;

            orbtargetList.Add(target);
        }

        // Create and position the floor.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.position = new Vector3(0.0f, -10.5f, 0.0f);
        floor.transform.localScale = new Vector3(25.0f, 1.0f, 25.0f);
    }

    // Update is called once per frame
    void Update()
    {

        // foreach (var point in orbList) 
        // {
        //     point.transform.position = RotateAroundOrigin(point.transform.position, 100 * Time.deltaTime);        
        // }

        int i = 0;
        // iterate through each point and move each orb
        foreach (var point in orbList) {
            float step = speed * Time.deltaTime;
            // transform.position = Vector3.MoveTowards(transform.position, target.position, step);
            // check the current state of the orb target 
            // if 0 it is moving towards origin
            // otherwise it is moving away from origin
            // away from origin target is 2 times the size of each respective orb's instantiation point
            
            GameObject target = orbtargetList[i];
            point.transform.position = Vector3.MoveTowards(point.transform.position, target.transform.position, step);

            if (orb_states[i] == 0) 
            {
                if (Vector3.Distance(point.transform.position, target.transform.position) < 0.2f)
                {
                    target.transform.position = new Vector3((float)(list[i,0]),(float)(list[i,1]),(float)(list[i,2]));
                    target.transform.position = RotateAroundOrigin(target.transform.position,-90);
                    target.transform.position *= orb_distance*2.5f;

                    orbtargetList[i].transform.position = target.transform.position;
                    orb_states[i] = 1;
                }
            } 
            if (orb_states[i] == 1)
            {
                if (Vector3.Distance(point.transform.position, target.transform.position) < 0.1f)
                {
                    target.transform.position = new Vector3((float)(list[i,0]),(float)(list[i,1]),(float)(list[i,2]));
                    target.transform.position = RotateAroundOrigin(target.transform.position,-90);
                    target.transform.position *= orb_distance*1.0f;

                    orbtargetList[i].transform.position = target.transform.position;
                    orb_states[i] = 0;

                }
            }
            i += 1;

        }
    }

    Vector3 RotateAroundOrigin(Vector3 startPos,float angle)
    {
        startPos.Normalize();
        Quaternion rot = Quaternion.Euler(angle, 0.0f, 0.0f); // Rotate [angle] degrees about the x axis.
        startPos = rot * startPos;
        return startPos;
    }   

    Vector3 ResizeHead(Vector3 coord, int size)
    {
        coord = size * coord;
        return coord;
    }
}

