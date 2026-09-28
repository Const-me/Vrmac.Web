namespace Vrmac.Admin;

/// <summary>Apply to methods for custom colours of the menu items</summary>
[AttributeUsage( AttributeTargets.Method )]
public sealed class CommandColourAttribute: Attribute
{
	internal readonly uint bgr;
	/// <summary>Apply to command methods for custom colours of the menu items</summary>
	public CommandColourAttribute( uint bgr ) => this.bgr = bgr;
}