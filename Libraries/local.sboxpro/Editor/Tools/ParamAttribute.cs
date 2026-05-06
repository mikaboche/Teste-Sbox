using System;

namespace SboxPro;

[AttributeUsage( AttributeTargets.Method, AllowMultiple = true )]
public sealed class ParamAttribute : Attribute
{
	public string Name { get; }
	public string Description { get; }
	public string Type { get; set; } = "string";
	public bool Required { get; set; } = false;
	public string Default { get; set; }
	public string Enum { get; set; }

	public ParamAttribute( string name, string description )
	{
		Name = name;
		Description = description;
	}
}
