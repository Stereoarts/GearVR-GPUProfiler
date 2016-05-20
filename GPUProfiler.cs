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

	string _gpu_maxFreq;

	float _fps = 0.0f;
	int _fpsCount = 0;
	float _updateTime = 0.0f;
	Vector2 _cachedSizeDelta = Vector2.zero;

	const float PixelsPerUnit = 8000.0f;

	DeviceModel _deviceModel = DeviceModel.Unknown;

	public enum DeviceModel
	{
		Unknown = -1,
		SCV31,	// Galaxy S6
		SCV33,	// Galaxy S7
	}

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

		string deviceModel = SystemInfo.deviceModel;
		if( deviceModel != null ) {
			switch( deviceModel ) {
			case "samsung SCV31":
				_deviceModel = DeviceModel.SCV31;
                break;
			case "samsung SCV33":
				_deviceModel = DeviceModel.SCV33;
				break;
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

		if( _deviceModel == DeviceModel.SCV31 ) {
			_gpu_maxFreq = _GetGPUFreq( GPUFreqType.Max );
		}
		if( _deviceModel == DeviceModel.SCV33 ) {
			_gpu_maxFreq = _GetGPUFreq( GPUFreqType.Max );
		}

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
			if( _deviceModel != DeviceModel.Unknown ) {
				str.AppendLine( _GetDeviceName() );
			}
			str.Append( "FPS : " );
			str.AppendLine( _fps.ToString() );
			str.Append( "CPU Temp : " );
			str.AppendLine( _GetCPUTemp() );
			str.Append( "GPU Busy : " );
			str.AppendLine( _GetGPUBusy() );
			str.Append( "GPU Clock : " );
			str.Append( _GetGPUFreq( GPUFreqType.Cur ) );
			str.Append( " / " );
			str.Append( _gpu_maxFreq );
			str.AppendLine( "" );
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

	string _GetCPUTemp()
	{
		string path = "/sys/devices/virtual/thermal/thermal_zone0/temp";

		string tempStr = _GetFileAllText_NoReturn( path );
		int temp;
		int divider = (_deviceModel == DeviceModel.SCV33) ? 10 : 1000;
        if( int.TryParse( tempStr, out temp ) ) {
			return (temp / divider).ToString();
		}

		return "-";
	}

	enum GPUFreqType
	{
		Cur,
		Max,
	}

	string _GetDeviceName()
	{
		switch( _deviceModel ) {
		case DeviceModel.SCV31: return "Galaxy S6";
		case DeviceModel.SCV33: return "Galaxy S7";
		}

		return "-";
	}

	string _GetGPUFreq( GPUFreqType gpuFreqType )
	{
		if( _deviceModel == DeviceModel.Unknown ) {
			return "-";
		}
			  
		string path = "";

		if( _deviceModel == DeviceModel.SCV31 ) {
			switch( gpuFreqType ) {
			case GPUFreqType.Cur: path = "/sys/devices/platform/gpusysfs/gpu_clock"; break;
			case GPUFreqType.Max: path = "/sys/devices/platform/gpusysfs/gpu_max_clock"; break;
			}
		}
		if( _deviceModel == DeviceModel.SCV33 ) {
			switch( gpuFreqType ) {
			case GPUFreqType.Cur: path = "/sys/class/kgsl/kgsl-3d0/gpuclk"; break;
			case GPUFreqType.Max: path = "/sys/class/kgsl/kgsl-3d0/max_gpuclk"; break;
			}
		}

		string tempStr = _GetFileAllText_NoReturn( path );
		int temp;
		int divider = (_deviceModel == DeviceModel.SCV33) ? 1000000 : 1;
		if( int.TryParse( tempStr, out temp ) ) {
			return (temp / divider).ToString();
		} else {
			return "-";
		}
	}

	string _GetGPUBusy()
	{
		if( _deviceModel == DeviceModel.SCV31 ) {
			return _GetFileAllText_NoReturn( "/sys/devices/platform/gpusysfs/gpu_busy" );
        }
		if( _deviceModel == DeviceModel.SCV33 ) {
			string str = _GetFileAllText_NoReturn( "/sys/class/kgsl/kgsl-3d0/gpubusy" );
			string[] splitStr = str.Split(' ');
			if( splitStr != null && splitStr.Length == 2 ) {
				int v0, v1;
				if( int.TryParse( splitStr[0], out v0 ) && int.TryParse( splitStr[1], out v1 ) ) {
					return (((long)v0 * 100) / (long)v1).ToString();
				}
			}
			return str; // Failsafe.
		}

		return "-";
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
