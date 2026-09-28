using System.Runtime.CompilerServices;
// DB converter tool needs access to internal types for the GeoPackage.Header structure
[assembly: InternalsVisibleTo( "Vrmac.MaxMind" )]