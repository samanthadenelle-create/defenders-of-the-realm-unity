using UnityEngine;
using System.Collections;

public class Dragon : MonoBehaviour {
    Animator dragon;
    private IEnumerator coroutine;
	// Use this for initialization
	void Start () {
        dragon = GetComponent<Animator>();
	}
	
	// Update is called once per frame
	void Update () {
        if (Input.GetKey(KeyCode.Alpha1))
        {
            dragon.SetBool("walk", true);
            dragon.SetBool("run", false);
            dragon.SetBool("idle", false);
        }
        if (Input.GetKey(KeyCode.Alpha2))
        {
            dragon.SetBool("run", true);
            dragon.SetBool("walk", false);

        }
        if (Input.GetKey(KeyCode.Alpha3))
        {
            dragon.SetBool("takeoff", true);
            dragon.SetBool("run", false);
            dragon.SetBool("walk", false);
            StartCoroutine("fly");
            fly();

        }
        if (Input.GetKey(KeyCode.Alpha4))
        {
            dragon.SetBool("fly", true);
            dragon.SetBool("takeoff", false);
            dragon.SetBool("attack1", false);
            dragon.SetBool("glide", false);

        }
        if (Input.GetKey(KeyCode.Alpha5))
        {
            dragon.SetBool("attack1", true);
            dragon.SetBool("fly", false);
            dragon.SetBool("glide", false);
            StartCoroutine("flame");
            flame();

        }
        if (Input.GetKey(KeyCode.Alpha6))
        {
            dragon.SetBool("glide", true);
            dragon.SetBool("attack1", false);
            dragon.SetBool("fly", false);

        }
        if (Input.GetKey(KeyCode.Alpha7))
        {
            dragon.SetBool("attack2", true);
            dragon.SetBool("glide", false);
            StartCoroutine("flame2");
            flame2();

        }
        if (Input.GetKey(KeyCode.Alpha8))
        {
            dragon.SetBool("landing", true);
            dragon.SetBool("attack2", false);
            dragon.SetBool("glide", false);
            dragon.SetBool("fly", false);

        }
        if (Input.GetKey(KeyCode.Alpha9))
        {
            dragon.SetBool("bite", true);
            dragon.SetBool("landing", false);
            dragon.SetBool("idle", false);
            StartCoroutine("idle");
            idle();

        }
        if (Input.GetKey(KeyCode.Alpha0))
        {
            dragon.SetBool("idle", true);
            dragon.SetBool("bite", false);

        }
        if (Input.GetKey(KeyCode.Keypad0))
        {
            dragon.SetBool("attack3", true);
            dragon.SetBool("idle", false);
            StartCoroutine("flame3");
            flame3();

        }
        if (Input.GetKey(KeyCode.Keypad1))
        {
            dragon.SetBool("hit", true);
            dragon.SetBool("attack3", false);
            dragon.SetBool("idle", false);
            StartCoroutine("idle2");
            idle2();

        }
        if (Input.GetKey(KeyCode.Keypad2))
        {
            dragon.SetBool("die", true);
            dragon.SetBool("hit", false);
            dragon.SetBool("idle", false);
            StartCoroutine("idle3");
            idle3();

        }
        if (Input.GetKey(KeyCode.Keypad3))
        {
            dragon.SetBool("die2", true);
            dragon.SetBool("glide", false);

        }
    }
    IEnumerator fly()
    {
        yield return new WaitForSeconds(1.3f);
        dragon.SetBool("fly", true);
        dragon.SetBool("takeoff", false);
    }
    IEnumerator idle()
    {
        yield return new WaitForSeconds(1.0f);
        dragon.SetBool("idle", true);
        dragon.SetBool("bite", false);
    }
    IEnumerator idle2()
    {
        yield return new WaitForSeconds(0.3f);
        dragon.SetBool("idle", true);
        dragon.SetBool("hit", false);
    }
    IEnumerator idle3()
    {
        yield return new WaitForSeconds(2.0f);
        dragon.SetBool("idle", true);
        dragon.SetBool("die", false);
    }
    IEnumerator flame()
    {
        yield return new WaitForSeconds(3.2f);
        dragon.SetBool("fly", true);
        dragon.SetBool("attack1", false);
    }
    IEnumerator flame2()
    {
        yield return new WaitForSeconds(2.28f);
        dragon.SetBool("glide", true);
        dragon.SetBool("attack2", false);
    }
    IEnumerator flame3()
    {
        yield return new WaitForSeconds(2.58f);
        dragon.SetBool("idle", true);
        dragon.SetBool("attack3", false);
    }

}
