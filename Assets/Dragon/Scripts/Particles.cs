using UnityEngine;
using System.Collections;

public class Particles : MonoBehaviour {
    public ParticleSystem fire;
    private IEnumerator coroutine;
	// Use this for initialization
	void Start () {
        fire = GetComponent<ParticleSystem>();
	}
	
	// Update is called once per frame
	void Update () {
        if (Input.GetKey(KeyCode.Alpha5))
        {
            StartCoroutine("fireon");
            fireon();

        }
        if (Input.GetKey(KeyCode.Alpha7))
        {
            StartCoroutine("fireon2");
            fireon2();

        }
        if (Input.GetKey(KeyCode.Keypad0))
        {
            StartCoroutine("fireon3");
            fireon3();

        }

    }
    IEnumerator fireon()
    {
        yield return new WaitForSeconds(1.00f);
        fire.GetComponent<ParticleSystem>().enableEmission = true;
        StartCoroutine("fireoff");
        fireoff();
    }
    IEnumerator fireoff()
    {
        yield return new WaitForSeconds(2.00f);
        fire.GetComponent<ParticleSystem>().enableEmission = false;
    }
    IEnumerator fireon2()
    {
        yield return new WaitForSeconds(1.00f);
        fire.GetComponent<ParticleSystem>().enableEmission = true;
        StartCoroutine("fireoff2");
        fireoff2();
    }
    IEnumerator fireoff2()
    {
        yield return new WaitForSeconds(1.4f);
        fire.GetComponent<ParticleSystem>().enableEmission = false;
    }
    IEnumerator fireon3()
    {
        yield return new WaitForSeconds(0.5f);
        fire.GetComponent<ParticleSystem>().enableEmission = true;
        StartCoroutine("fireoff3");
        fireoff3();
    }
    IEnumerator fireoff3()
    {
        yield return new WaitForSeconds(2.00f);
        fire.GetComponent<ParticleSystem>().enableEmission = false;
    }


}
