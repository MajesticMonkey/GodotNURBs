using Godot;
using Godot.Collections;
using System;
using NURBs;

namespace NURBs
{
	[GlobalClass, Tool]
	public partial class ControlPoint : MeshInstance3D
	{
		public Vector2 Loc = new Vector2(0, 0);

		public bool WouldCreateOverlappingSurface = false;

		public bool HasSurface = false;

		public string HasSurfaceMetaName = "HasSurface";

		public string NamePrefix = "";

		public NURBBuilder Builder;

		public Callable PListChange;

		[Export]
		public float Weight = 1.0f;

		public ControlPoint()
		{

		}

		public ControlPoint(string Prefix, Vector3 LocAndWeight)
		{
			NamePrefix = Prefix;
			SetNameWithUVW(LocAndWeight);
		}

		public override void _EnterTree()
		{
			Vector3 Forklift = LocFromName();
			Loc = new Vector2(Forklift.X, Forklift.Y);
			Weight = Forklift.Z;
			PListChange = Callable.From(_OnPropertyChanged);
			if (!IsConnected(SignalName.PropertyListChanged, PListChange))
			{
				this.Connect(SignalName.PropertyListChanged, PListChange);
			}
		}

		public override void _ExitTree()
		{
			if (IsConnected(SignalName.PropertyListChanged, PListChange))
			{
				this.Disconnect(SignalName.PropertyListChanged, PListChange);
			}
		}

		public void _OnPropertyChanged()
		{
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
				NURBBuilder Builder = GetParent();

				HasSurface = !HasSurface;

				if (HasSurface) { Builder.CreateSurfaceExternally(Loc); } else { Builder.RemoveSurfaceExternally(Loc); }

				char LastChar = ((string)Name)[^1];

				if (HasSurface ^ (LastChar == '_')) { Name = LastChar == '_' ? ((string)Name)[..^1] : ((string)Name) + '_'; }
			}
			NotifyPropertyListChanged();
		}

		public Vector4 GetPosVec()
		{
			return new Vector4(Position.X, Position.Y, Position.Z, Weight);
		}

		public new NURBBuilder GetParent()
		{
			var forklift = base.GetParent();
			if (forklift is NURBBuilder builder)
			{
				return builder;
			} else {
				throw new InvalidOperationException("This control point has somehow been unparented from a NURBBuilder. Please reload your project and hope for the best.");
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
			string NameForklift = NamePrefix + UVW.X.ToString() + "_" + UVW.Y.ToString() + "_" + UVW.Z.ToString();
			if (HasSurface) { NameForklift += "_"; }
			Name = NameForklift;
		}
	}
}
