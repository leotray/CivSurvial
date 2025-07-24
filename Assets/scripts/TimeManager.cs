using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    [Header("Skybox Textures")]
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    [Header("Time Settings")]
    public float speed = 1f;

    [Header("Lighting")]
    [SerializeField] private Gradient graddientNightToSunrise;
    [SerializeField] private Gradient graddientSunriseToDay;
    [SerializeField] private Gradient graddientDayToSunset;
    [SerializeField] private Gradient graddientSunsetToNight;
    [SerializeField] private Light globalLight;

    private int minutes;
    public int Minutes
    {
        get => minutes;
        set
        {
            minutes = value;
            OnMinutesChange(value);
        }
    }

    private int hours = 5;
    public int Hours
    {
        get => hours;
        set
        {
            hours = value;
            OnHoursChange(value);
        }
    }

    private int days;
    public int Days
    {
        get => days;
        set => days = value;
    }

    private float tempSecond;
    private int lastTransitionHour = -1;
    private Coroutine skyboxCoroutine;
    private Coroutine lightCoroutine;

    private void Update()
    {
        tempSecond += Time.deltaTime * speed;

        if (tempSecond >= 1f)
        {
            Minutes += 1;
            tempSecond = 0f;
        }
    }

    private void OnMinutesChange(int value)
    {
        globalLight.transform.Rotate(Vector3.up, (1f / (1440f / 4f)) * 360f, Space.World);
        if (value >= 60)
        {
            Hours++;
            minutes = 0;
        }
        if (Hours >= 24)
        {
            Hours = 0;
            Days++;
        }
    }

    private void OnHoursChange(int value)
    {
        if (value == lastTransitionHour) return;
        lastTransitionHour = value;

        if (value == 6)
        {
            StartSkyboxTransition(skyboxNight, skyboxSunrise, graddientNightToSunrise);
        }
        else if (value == 8)
        {
            StartSkyboxTransition(skyboxSunrise, skyboxDay, graddientSunriseToDay);
        }
        else if (value == 18)
        {
            StartSkyboxTransition(skyboxDay, skyboxSunset, graddientDayToSunset);
        }
        else if (value == 22)
        {
            StartSkyboxTransition(skyboxSunset, skyboxNight, graddientSunsetToNight);
        }
    }

    private void StartSkyboxTransition(Texture2D from, Texture2D to, Gradient lightGradient)
    {
        if (skyboxCoroutine != null)
            StopCoroutine(skyboxCoroutine);
        if (lightCoroutine != null)
            StopCoroutine(lightCoroutine);

        skyboxCoroutine = StartCoroutine(LerpSkybox(from, to, 10f));
        lightCoroutine = StartCoroutine(LerpLight(lightGradient, 10f));
    }

    private IEnumerator LerpSkybox(Texture2D a, Texture2D b, float time)
    {
        RenderSettings.skybox.SetTexture("_Texture1", a);
        RenderSettings.skybox.SetTexture("_Texture2", b);
        RenderSettings.skybox.SetFloat("_Blend", 0f);

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            RenderSettings.skybox.SetFloat("_Blend", t / time);
            yield return null;
        }

        RenderSettings.skybox.SetTexture("_Texture1", b);
        RenderSettings.skybox.SetFloat("_Blend", 0f);
    }

    private IEnumerator LerpLight(Gradient lightGradient, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Color color = lightGradient.Evaluate(t / time);
            globalLight.color = color;
            RenderSettings.fogColor = color;
            yield return null;
        }
    }
}
