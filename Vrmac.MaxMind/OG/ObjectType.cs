// Adapted from MaxMind-DB-Reader-dotnet 5.0, slightly modified on 2026-05-13
namespace MaxMind.Db;

/// <summary>Enumeration representing the types of objects read from the database</summary>
enum ObjectType: byte
{
	Extended,
	Pointer,
	Utf8String,
	Double,
	Bytes,
	Uint16,
	Uint32,
	Map,
	Int32,
	Uint64,
	Uint128,
	Array,
	Container,
	EndMarker,
	Boolean,
	Float
}