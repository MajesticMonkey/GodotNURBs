using Godot;
using Godot.Collections;
using System;
using BezierSurfaces;

namespace BezierSurfaces
{
	[GlobalClass, Tool]
	public partial class ControlPoint : MeshInstance3D
	{
		public Vector2 Loc = new Vector2(0, 0);

		public bool HasSurface = false;

		public string HasSurfaceMetaName = "HasSurface";

		[Export]
		public float Weight = 1.0f;
		
		public override void _EnterTree()
		{
			PropertyListChanged += _OnPropertyChanged;
		}

		public void _OnPropertyChanged()
		{
			var builder = GetParent();

			SetNameWithUVW(new Vector3(Loc.X, Loc.Y, Weight));
		}

		public void CreateSurface()
		{
			ToggleSurface(true);
		}

		public void RemoveSurface()
		{
			ToggleSurface(false);
		}

		public void ToggleSurface(bool Add)
		{
			if (HasSurface ^ Add)
			{
				BezierSurfaceBuilder builder = GetParent();

				HasSurface = !HasSurface;

				if (HasSurface) { builder.CreateSurfaceExternally(Loc); } else { builder.RemoveSurfaceExternally(Loc); }

				char LastChar = ((string)Name)[^1];

				if (HasSurface ^ (LastChar == '_')) { Name = LastChar == '_' ? ((string)Name)[..^1] : ((string)Name) + '_'; }
			}
			NotifyPropertyListChanged();
		}

		public Vector4 GetPosVec()
		{
			return new Vector4(Position.X, Position.Y, Position.Z, Weight);
		}

		public new BezierSurfaceBuilder GetParent()
		{
			var forklift = base.GetParent();
			if (forklift is BezierSurfaceBuilder builder)
			{
				return builder;
			} else {
				throw new InvalidOperationException("This control point has somehow been unparented from a BezierSurfaceBuilder. Please reload your project and hope for the best.");
			}
		}

		public Vector3 LocFromName()
		{
			Vector3 Location = new Vector3(0, 0, 0);
			string NameForklift = this.Name;
			
			string[] SplitName = NameForklift.Split("_");
			Location.X = float.Parse(SplitName[1]);
			Location.Y = float.Parse(SplitName[2]);
			Location.Z = float.Parse(SplitName[3]);
			return Location;
		}

		public void SetNameWithUVW(Vector3 UVW)
		{
			string Prefix = GetParent().NodePrefix;
			string NameForklift = Prefix + UVW.X.ToString() + "_" + UVW.Y.ToString() + "_" + UVW.Z.ToString();
			if (HasSurface) { NameForklift += "_"; }
			Name = NameForklift;
		}
	}
}
