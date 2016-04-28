using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Text;

[ExecuteInEditMode]
public class GPUProfiler : MonoBehaviour
{
	public Font font;
	public Material fontMaterial;
	public float updateInterval = 0.25f;
	public float fontSize = 0.025f;
	public RenderMode renderMode = RenderMode.WorldSpace;

	RectTransform _canvasRectTransform;
	
	Text _text;
	RectTransform _textRectTransform;

	string _gpu_minFreq;
	string _gpu_maxFreq;

	float _fps = 0.0f;
	int _fpsCount = 0;
	float _updateTime = 0.0f;
	Vector2 _cachedSizeDelta = Vector2.zero;

	const float PixelsPerUnit = 8000.0f;

	void Awake()
	{
		if( !Application.isPlaying ) {
			if( this.font == null ) {
				#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4 || UNITY_4_5 || UNITY_4_6
				#else
				this.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
				if( this.font == null ) {
					this.font = Font.CreateDynamicFontFromOSFont("Arial Bold", 16);
				}
				#endif
			}
			if( this.fontMaterial == null || this.fontMaterial.shader == null ) {
				Shader shader = Shader.Find("UI/GPUProfiler");
				if( shader != null ) {
					this.fontMaterial = new Material( shader );
				}
			}
		}
	}

	void Start()
	{
		if( !Application.isPlaying ) {
			return;
		}

		Canvas canvas = this.gameObject.AddComponent<Canvas>();
		canvas.renderMode = renderMode;
		canvas.pixelPerfect = true;

		CanvasScaler canvasScaler = this.gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;

		_canvasRectTransform = this.gameObject.GetComponent<RectTransform>();

		if( renderMode == RenderMode.WorldSpace ) {
			canvasScaler.dynamicPixelsPerUnit = PixelsPerUnit;
			canvasScaler.referencePixelsPerUnit = PixelsPerUnit;
			_canvasRectTransform.sizeDelta = new Vector2( 1.0f, 0.5f );
		}

		GameObject go = new GameObject ();
		go.transform.parent = this.transform;
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;
		Text text = go.AddComponent<Text>();
		text.font = this.font;
		text.material = this.fontMaterial;
		RectTransform rectTransform = go.GetComponent<RectTransform> ();

		_text = text;
		_textRectTransform = rectTransform;

		_gpu_minFreq = _GetFileAllText_NoReturn ("/sys/devices/platform/gpusysfs/gpu_min_clock");
		_gpu_maxFreq = _GetFileAllText_NoReturn ("/sys/devices/platform/gpusysfs/gpu_max_clock");

		if( renderMode == RenderMode.WorldSpace ) {
			_textRectTransform.sizeDelta = _canvasRectTransform.sizeDelta;
			_text.fontSize = 32;
			this.fontMaterial.SetFloat( "_OutlineOffset", 0.01f );
		} else {
			this.fontMaterial.SetFloat( "_OutlineOffset", 0.0025f );
		}
	}

	void Update()
	{
		if( !Application.isPlaying ) {
			return;
		}

		float updateTime = _updateTime;
		_updateTime += Time.deltaTime;
		if( _updateTime > this.updateInterval ) {
			_fps = (float)_fpsCount / Mathf.Max( updateTime, 0.0001f );
			_fpsCount = 0;
			_updateTime = 0.0f;
			StringBuilder str = new StringBuilder();
			str.Append( "FPS : " );
			str.AppendLine( _fps.ToString() );
			_DumpThermal( str, "CPU Temp : ", "/sys/devices/virtual/thermal/thermal_zone0/temp" );
			//_DumpThermal( str, "REG Temp : ", "/sys/devices/virtual/thermal/thermal_zone1/temp" );
			_DumpThermal( str, "BAT Temp : ", "/sys/devices/virtual/thermal/thermal_zone2/temp" );
			str.Append( "GPU Busy : " );
			str.AppendLine( _GetFileAllText_NoReturn( "/sys/devices/platform/gpusysfs/gpu_busy" ) );
			str.Append( "GPU Clock : " );
			str.Append( _GetFileAllText_NoReturn( "/sys/devices/platform/gpusysfs/gpu_clock" ) );
			str.Append( " ( " );
			str.Append( _gpu_minFreq );
			str.Append( " / " );
			str.Append( _gpu_maxFreq );
			str.AppendLine( " )" );
			str.Append( "GPU Voltage : " );
			str.AppendLine( _GetFileAllText_NoReturn( "/sys/devices/platform/gpusysfs/gpu_voltage" ) );
			_text.text = str.ToString();
		} else {
			++_fpsCount;
		}

		Vector2 sizeDelta = _canvasRectTransform.sizeDelta;
		if( _cachedSizeDelta != sizeDelta ) {
			if( renderMode == RenderMode.WorldSpace ) {
				// Nothing.
			} else {
				_textRectTransform.sizeDelta = sizeDelta;
				_text.fontSize = (int)(Mathf.Min( sizeDelta.x, sizeDelta.y ) * this.fontSize);
			}
		}
	}

	static void _DumpThermal( StringBuilder str, string name, string path )
	{
		str.Append( name );
		string tempStr = _GetFileAllText_NoReturn( path );
		int temp;
		if( int.TryParse( tempStr, out temp ) ) {
			str.AppendLine( (temp / 1000).ToString() );
		} else {
			str.AppendLine( "-" );
		}
	}

	static string _GetFileAllText_NoReturn( string path )
	{
		#if !UNITY_ANDROID || UNITY_EDITOR
		return "";
		#else
		try {
			using( StreamReader sr = new StreamReader( path, System.Text.ASCIIEncoding.ASCII ) ) {
				string r = sr.ReadToEnd();
				int startIndex = 0, length = r.Length;
				while( length != 0 ) {
					if( r[startIndex] == ' ' ) {
						++startIndex;
						length -= 1;
					} else {
						break;
					}
				}
				while( length != 0 ) {
					char c = r[startIndex + length - 1];
					if( c == '\r' || c == '\n' ) {
						length -= 1;
					} else {
						break;
					}
				}
				if( startIndex != 0 || length != r.Length ) {
					return r.Substring( startIndex, length );
				} else {
					return r;
				}
			}
		} catch( System.Exception ) {
			return "";
		}
		#endif
	}

}
