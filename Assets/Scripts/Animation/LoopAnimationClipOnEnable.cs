using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class LoopAnimationClipOnEnable : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip clip;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool randomStartTime;
    [SerializeField] private float speed = 1f;

    private PlayableGraph graph;
    private AnimationClipPlayable playable;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!loop || clip == null || clip.length <= 0f || !playable.IsValid())
        {
            return;
        }

        double currentTime = playable.GetTime();
        if (currentTime >= clip.length)
        {
            playable.SetTime(currentTime % clip.length);
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        Stop();
    }

    public void Play()
    {
        if (animator == null || clip == null)
        {
            return;
        }

        Stop();

        graph = PlayableGraph.Create($"LoopClip_{name}");
        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(Mathf.Max(0.01f, speed));

        if (randomStartTime && clip.length > 0f)
        {
            playable.SetTime(Random.Range(0f, clip.length));
        }

        output.SetSourcePlayable(playable);
        graph.Play();
    }

    public void Stop()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }

        playable = default;
    }
}
