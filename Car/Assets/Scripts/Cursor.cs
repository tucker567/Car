using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
public class CursorManager : MonoBehaviour
{
   public Texture2D defaultCursor;
   public Texture2D clickCursor;
   public Vector2 hotSpot = Vector2.zero;
   public CursorMode cursorMode = CursorMode.Auto;
   [Tooltip("Force software cursor if hardware cursor doesn't update on your platform.")]
   public bool forceSoftwareCursor = false;

   [Header("Scaling")]
   [Range(0.5f, 4f)] public float cursorScale = 1f;
   [Tooltip("Show an in-game debug slider to adjust size")] public bool showDebugSlider = false;
   [Tooltip("Use point filtering (crisp) when scaling")] public bool usePointFilter = true;

   [Header("Idle Hide")]
   [Tooltip("Hide cursor after no movement for this many seconds")]
   [Range(0.1f, 30f)] public float idleSeconds = 3f;
   public bool hideWhenIdle = true;

   Texture2D _scaledDefault;
   Texture2D _scaledClick;
   bool _isPressed;
   Texture2D _currentBase;
   Vector2 _lastMousePos;
   bool _hasMousePos;
   float _lastInputTime;
   bool _cursorHidden;


   void OnEnable()
   {
       ApplyCursor(defaultCursor);
       _hasMousePos = false;
       _lastInputTime = Time.unscaledTime;
       _cursorHidden = false;
       Cursor.visible = true;
   }
   void Start()
   {
       ApplyCursor(defaultCursor);
   }
   void Update()
   {
       // Track movement and clicks to manage idle hide/show
       bool moved = false;
       bool anyInput = false;
#if ENABLE_INPUT_SYSTEM
       if (Mouse.current != null)
       {
           Vector2 pos = Mouse.current.position.ReadValue();
           if (!_hasMousePos)
           {
               _lastMousePos = pos;
               _hasMousePos = true;
           }
           moved = (pos - _lastMousePos).sqrMagnitude > 0.01f;
           anyInput = moved || Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame;
           _lastMousePos = pos;
       }
#else
       Vector2 pos = Input.mousePosition;
       if (!_hasMousePos)
       {
           _lastMousePos = pos;
           _hasMousePos = true;
       }
       moved = (pos - _lastMousePos).sqrMagnitude > 0.01f;
       anyInput = moved || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0);
       _lastMousePos = pos;
#endif

       if (anyInput)
       {
           _lastInputTime = Time.unscaledTime;
           if (_cursorHidden)
           {
               // Show instantly on movement / click
               _cursorHidden = false;
               Cursor.visible = true;
               ApplyCursor(_isPressed ? clickCursor : defaultCursor);
           }
       }

       if (hideWhenIdle)
       {
           if (Time.unscaledTime - _lastInputTime >= idleSeconds)
           {
               if (!_cursorHidden)
               {
                   _cursorHidden = true;
                   Cursor.visible = false;
               }
           }
       }

       // Change cursor while left mouse is held
#if ENABLE_INPUT_SYSTEM
       if (Mouse.current != null)
       {
           if (Mouse.current.leftButton.wasPressedThisFrame)
           {
               _isPressed = true;
               if (!_cursorHidden) ApplyCursor(clickCursor);
           }
           else if (Mouse.current.leftButton.wasReleasedThisFrame)
           {
               _isPressed = false;
               if (!_cursorHidden) ApplyCursor(defaultCursor);
           }
       }
#else
       if (Input.GetMouseButtonDown(0))
       {
           _isPressed = true;
           if (!_cursorHidden) ApplyCursor(clickCursor);
       }
       else if (Input.GetMouseButtonUp(0))
       {
           _isPressed = false;
           if (!_cursorHidden) ApplyCursor(defaultCursor);
       }
#endif
   }

   void OnGUI()
   {
       // Debug scale slider
       if (showDebugSlider)
       {
           const float w = 200f;
           const float h = 20f;
           const float pad = 10f;
           GUI.Label(new Rect(pad, pad, w, h), $"Cursor Scale: {cursorScale:0.00}x");
           float newScale = GUI.HorizontalSlider(new Rect(pad, pad + h, w, h), cursorScale, 0.5f, 4f);
           if (Mathf.Abs(newScale - cursorScale) > 0.0001f)
           {
               SetCursorScale(newScale);
           }
       }
   }

   void OnApplicationFocus(bool hasFocus)
   {
       if (hasFocus)
           ApplyCursor(defaultCursor);
   }

   void OnApplicationPause(bool isPaused)
   {
       if (!isPaused)
           ApplyCursor(defaultCursor);
   }

   void OnDisable()
   {
       // Reset to system cursor when this component is disabled
       Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
       if (_scaledDefault) Destroy(_scaledDefault);
       if (_scaledClick) Destroy(_scaledClick);
       _scaledDefault = null;
        _scaledClick = null;
       Cursor.visible = true;
   }

   void ApplyCursor(Texture2D tex)
   {
       if (tex == null)
           return;
       _currentBase = tex;

        var scaled = GetScaledFor(tex);
        var mode = forceSoftwareCursor ? CursorMode.ForceSoftware : cursorMode;

        var scaleFactor = Mathf.Max(0.01f, cursorScale);
        var scaledHotspot = new Vector2(hotSpot.x * scaleFactor, hotSpot.y * scaleFactor);
        var clampedHotspot = new Vector2(
            Mathf.Clamp(scaledHotspot.x, 0, scaled.width - 1),
            Mathf.Clamp(scaledHotspot.y, 0, scaled.height - 1)
        );
        Cursor.SetCursor(scaled, clampedHotspot, mode);
   }

   public void SetCursorScale(float scale)
   {
       cursorScale = Mathf.Clamp(scale, 0.25f, 6f);
       if (_currentBase != null)
           ApplyCursor(_isPressed ? clickCursor : defaultCursor);
   }

   Texture2D GetScaledFor(Texture2D src)
   {
       if (src == null) return null;
       if (src == defaultCursor)
           return EnsureScaled(src, ref _scaledDefault);
       if (src == clickCursor)
           return EnsureScaled(src, ref _scaledClick);
       // Fallback: scale ad-hoc without caching
       return BuildScaled(src, cursorScale);
   }

   Texture2D EnsureScaled(Texture2D src, ref Texture2D cache)
   {
       int w = Mathf.Max(1, Mathf.RoundToInt(src.width * cursorScale));
       int h = Mathf.Max(1, Mathf.RoundToInt(src.height * cursorScale));
       if (cache != null && cache.width == w && cache.height == h)
           return cache;

       if (cache) Destroy(cache);
       cache = BuildScaled(src, cursorScale);
       return cache;
   }

   Texture2D BuildScaled(Texture2D src, float scale)
   {
       int w = Mathf.Max(1, Mathf.RoundToInt(src.width * scale));
        int h = Mathf.Max(1, Mathf.RoundToInt(src.height * scale));

       var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
       var prevActive = RenderTexture.active;
       var prevFilter = src.filterMode;
       src.filterMode = usePointFilter ? FilterMode.Point : FilterMode.Bilinear;
       Graphics.Blit(src, rt);
       RenderTexture.active = rt;
       var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
       tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
       tex.Apply();
       tex.filterMode = usePointFilter ? FilterMode.Point : FilterMode.Bilinear;
       tex.wrapMode = TextureWrapMode.Clamp;
       RenderTexture.active = prevActive;
       RenderTexture.ReleaseTemporary(rt);
       src.filterMode = prevFilter;
       return tex;
   }
}