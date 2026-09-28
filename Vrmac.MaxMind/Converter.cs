namespace Vrmac.MaxMind;

static class Converter
{
	/// <summary>Magic string for basic HTTP authentication, in base64 encoding</summary>
	internal const string envAuth = "MAXMIND_AUTH";

	/// <summary>File name for the converted GeoIP database</summary>
	internal const string result = "geoip.bin";
}