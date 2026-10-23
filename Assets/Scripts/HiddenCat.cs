using System.Collections;
using UnityEngine;

/// <summary>
/// 레벨에 숨겨진 고양이 한 마리입니다. 찾아야 하는 고양이(target)인지 옆집 고양이(decoy)인지 구분합니다.
/// </summary>
public class HiddenCat : MonoBehaviour
{
    public string catName;
    public bool isDecoy;
    public bool found;

    private static readonly string[] IdleStates = { "idle", "sit", "sitting" };

    private Animator animator;
    private AudioSource audioSource;

    public void Setup(string name, Color tint, bool decoy, AudioClip meow)
    {
        catName = name;
        isDecoy = decoy;

        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.clip = meow;
        audioSource.spatialBlend = 1f;          // 3D 사운드: 소리 나는 방향으로 찾을 수 있게
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 25f;

        // 사진 속 고양이와 비슷하게 색을 입힙니다 (머티리얼 복제 없이)
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_Color", tint);
            r.SetPropertyBlock(block);
        }

        if (animator != null)
        {
            animator.Play(IdleStates[Random.Range(0, IdleStates.Length)], 0, Random.value);
        }
    }

    public void Meow()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.pitch = isDecoy ? 0.8f : Random.Range(0.95f, 1.15f);
            audioSource.Play();
        }
        if (animator != null) animator.Play("miau", 0, 0f);
    }

    public void PlayFound()
    {
        found = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Meow();
        StartCoroutine(PopAndHide());
    }

    public void PlayRunAway()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (animator != null) animator.Play("walk", 0, 0f);
        StartCoroutine(RunAway());
    }

    IEnumerator PopAndHide()
    {
        Vector3 baseScale = transform.localScale;
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.7f;
            float bounce = 1f + Mathf.Sin(k * Mathf.PI) * 0.35f;
            transform.localScale = baseScale * bounce * (1f - k * k);
            transform.position += Vector3.up * Time.unscaledDeltaTime * 0.6f;
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator RunAway()
    {
        float t = 0f;
        Vector3 dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
        transform.rotation = Quaternion.LookRotation(dir);
        Vector3 baseScale = transform.localScale;
        while (t < 0.8f)
        {
            t += Time.unscaledDeltaTime;
            transform.position += dir * Time.unscaledDeltaTime * 2.5f;
            transform.localScale = baseScale * (1f - t / 0.8f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
