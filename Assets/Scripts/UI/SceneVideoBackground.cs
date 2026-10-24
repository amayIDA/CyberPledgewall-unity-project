using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Full-screen looping video on a UI RawImage. Uses the player's own texture (APIOnly)
/// so Android / GLES / Vulkan can display VideoClips without a custom RenderTexture.
/// </summary>
[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(AspectRatioFitter))]
[RequireComponent(typeof(VideoPlayer))]
public class SceneVideoBackground : MonoBehaviour
{
    [SerializeField] private VideoClip idleClip;
    [SerializeField] private VideoClip drawClip;

    private RawImage _image;
    private AspectRatioFitter _fitter;
    private VideoPlayer _player;
    private VideoClip _pending;
    private bool _drawMode;
    private bool _prepared;

    private void Awake()
    {
        _image = GetComponent<RawImage>();
        _fitter = GetComponent<AspectRatioFitter>();
        _player = GetComponent<VideoPlayer>();

        _image.color = Color.white;
        _image.raycastTarget = false;
        _fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        _fitter.aspectRatio = 16f / 9f;

        _player.playOnAwake = false;
        _player.isLooping = true;
        _player.skipOnDrop = true;
        _player.waitForFirstFrame = true;
        _player.renderMode = VideoRenderMode.APIOnly;
        _player.audioOutputMode = VideoAudioOutputMode.None;
        _player.source = VideoSource.VideoClip;
        _player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        _player.prepareCompleted += HandlePrepared;
        _player.errorReceived += HandleError;
        _player.started += HandleStarted;

        PlayIdle();
    }

    private void OnEnable()
    {
        if (_pending != null && _player != null && !_player.isPlaying)
            Begin(_pending);
    }

    private void LateUpdate()
    {
        if (_image == null || _player == null)
            return;

        Texture frame = _player.texture;
        if (frame != null && _image.texture != frame)
            _image.texture = frame;
    }

    private void OnDestroy()
    {
        if (_player == null)
            return;

        _player.prepareCompleted -= HandlePrepared;
        _player.errorReceived -= HandleError;
        _player.started -= HandleStarted;
    }

    public void PlayIdle()
    {
        _drawMode = false;
        Play(idleClip != null ? idleClip : drawClip);
    }

    public void PlayDraw()
    {
        _drawMode = true;
        Play(drawClip != null ? drawClip : idleClip);
    }

    public void SetDrawMode(bool draw)
    {
        if (draw)
            PlayDraw();
        else
            PlayIdle();
    }

    private void Play(VideoClip clip)
    {
        if (clip == null || _player == null)
            return;

        if (_pending == clip && (_player.isPlaying || _player.isPrepared))
            return;

        Begin(clip);
    }

    private void Begin(VideoClip clip)
    {
        _pending = clip;
        _prepared = false;
        _player.Stop();
        _player.clip = clip;
        ApplyAspect(clip);
        _player.Prepare();
    }

    private void HandlePrepared(VideoPlayer source)
    {
        _prepared = true;
        ApplyAspect(source.clip);
        if (_image != null && source.texture != null)
            _image.texture = source.texture;
        source.Play();
    }

    private void HandleStarted(VideoPlayer source)
    {
        if (_image != null && source.texture != null)
            _image.texture = source.texture;
    }

    private void HandleError(VideoPlayer source, string message)
    {
        Debug.LogError("Video background failed: " + message);
        VideoClip fallback = _drawMode ? idleClip : drawClip;
        if (fallback != null && fallback != _pending)
            Begin(fallback);
    }

    private void ApplyAspect(VideoClip clip)
    {
        if (_fitter == null || clip == null || clip.height < 1)
            return;

        _fitter.aspectRatio = clip.width / (float)clip.height;
    }
}
