using UnityEngine;
using System.Collections;

public class target : MonoBehaviour {
    Animator dragon;
    float speed = 0.2f;
    public Transform box;
    private IEnumerator coroutine;
	// Use this for initialization
	void Start () {
        box = GetComponent<Transform>();
	}
	
	// Update is called once per frame
	void Update () {

        if (Input.GetKey(KeyCode.Alpha3))
        {
            box.transform.position = Vector3.Lerp(transform.position, new Vector3(0, 4, 0), Time.deltaTime*speed);
        }
        if (Input.GetKey(KeyCode.Alpha8))
        {
            box.transform.position = Vector3.Lerp(transform.position, new Vector3(0, 1, 0), Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.Keypad3))
        {
            box.transform.position = Vector3.Lerp(transform.position, new Vector3(0, 1, 0), Time.deltaTime);
        }
     }

}
