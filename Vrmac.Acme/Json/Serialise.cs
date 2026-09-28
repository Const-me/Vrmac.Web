using System.Text.Json.Serialization;
namespace AcmeV2.Json;

[JsonSourceGenerationOptions( UseStringEnumConverter = true )]
[JsonSerializable( typeof( Endpoints ) )]
[JsonSerializable( typeof( Key ) )]
[JsonSerializable( typeof( NewAccountHeader ) )]
[JsonSerializable( typeof( NewAccountPayload ) )]
[JsonSerializable( typeof( OrderIdentifier ) )]
[JsonSerializable( typeof( NewOrderRequest ) )]
[JsonSerializable( typeof( Signed ) )]
[JsonSerializable( typeof( ProtectedHeader ) )]
[JsonSerializable( typeof( NewAccountResponse ) )]
[JsonSerializable( typeof( OrderIdentifier ) )]
[JsonSerializable( typeof( NewOrderResponse ) )]
[JsonSerializable( typeof( ChallengeResponse ) )]
[JsonSerializable( typeof( PollResponse ) )]
[JsonSerializable( typeof( CertificateRequest ) )]
[JsonSerializable( typeof( CertificateResponse ) )]
internal partial class Serialise: JsonSerializerContext { }