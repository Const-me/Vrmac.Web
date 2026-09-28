namespace AcmeV2;

enum eStatus: byte
{
	Invalid = 1,
	Pending = 2,
	Processing = 3,
	Valid = 4,

	Ready = Valid,
	Revoked = Invalid,
	Expired = Invalid,
	Deactivated = Invalid
}