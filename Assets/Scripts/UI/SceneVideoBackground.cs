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
    private bool _preparing;
    private bool _resumePlayback;

    private void Awake()
    {
        _image = GetComponent<RawImage>();
        _fitter = GetComponent<AspectRatioFitter>();
        _player = GetComponent<VideoPlayer>();

        _image.color = Color.white;
        _image.raycastTarget = false;
        _image.enabled = false; // Keep the authored fallback visible until a frame is ready.
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
        if (_pending != null && _player != null && !_player.isPlaying && !_preparing)
            Begin(_pending);
    }

    private void LateUpdate()
    {
        if (_image == null || _player == null)
            return;

        Texture frame = _player.texture;
        if (frame != null && _player.isPlaying && _player.frame >= 0)
        {
            if (_image.texture != frame) _image.texture = frame;
            _image.enabled = true;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (_player == null || _pending == null) return;
        if (paused)
        {
            _resumePlayback = true;
            _player.Pause();
        }
        else if (_resumePlayback)
        {
            _resumePlayback = false;
            if (_player.isPrepared) _player.Play(); else Begin(_pending);
        }
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

        if (_pending == clip && (_preparing || _player.isPlaying || _player.isPrepared))
            return;

        Begin(clip);
    }

    private void Begin(VideoClip clip)
    {
        _pending = clip;
        _preparing = true;
        if (_image != null) _image.enabled = false;
        _player.Stop();
        _player.clip = clip;
        ApplyAspect(clip);
        _player.Prepare();
    }

    private void HandlePrepared(VideoPlayer source)
    {
        _preparing = false;
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
        _preparing = false;
        if (_image != null) _image.enabled = false;
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
