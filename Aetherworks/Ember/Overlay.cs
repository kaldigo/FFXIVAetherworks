// Adapted from Browsingway 1.7.2, GPL-3.0. See vendor/Browsingway.
using Browsingway.Common.Ipc;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace Aetherworks.Ember;

internal class Overlay : IDisposable
{
	private readonly EmberOptions _overlayConfig;

	private readonly RenderProcess _renderProcess;
	private bool _captureCursor;
	private ImGuiMouseCursor _cursor;

	private bool _mouseInWindow;

	private bool _resizing;
	private Vector2 _size;
	private bool _hasRenderError = false;
	private SharedTextureHandler? _textureHandler;
	private Exception? _textureRenderException;
	private static readonly Guid OverlayId = new("9b96bebc-6e26-494e-8781-6cc953315d84");
	private const string EmberUrl = "http://proxy.iinact.com/overlay/ember_overlay/?HOST_PORT=ws://127.0.0.1:10501";
	private long _timeLastInCombat;

	public Overlay(RenderProcess renderProcess, EmberOptions overlayConfig)
	{
		_renderProcess = renderProcess;
		// TODO: handle that the correct way
		_renderProcess.Crashed += (_, _) =>
		{
			_size = Vector2.Zero;
			_resizing = false;
			_hasRenderError = true;
		};

		_overlayConfig = overlayConfig;
	}

	public Guid RenderGuid => OverlayId;

	public void Dispose()
	{
		_textureHandler?.Dispose();
		_ = _renderProcess.Rpc?.RemoveOverlay(RenderGuid);
	}

	public void Zoom(float zoom)
	{
		_ = _renderProcess.Rpc?.Zoom(RenderGuid, zoom);
	}

	public void SetCursor(Cursor cursor)
	{
		_captureCursor = cursor != Cursor.BrowsingwayNoCapture;
		_cursor = DecodeCursor(cursor);
	}

	public void Render()
	{
		if (HiddenByCombatFlags() ||
		    (_overlayConfig.HideInPvP && Services.ClientState.IsPvP))
		{
			_mouseInWindow = false;
			return;
		}

		ImGui.SetNextWindowSize(new Vector2(640, 480), ImGuiCond.FirstUseEver);
		ImGui.Begin($"Ember###{OverlayId}", GetWindowFlags());

		HandleWindowSize();

		if (_textureHandler != null && !_hasRenderError)
		{
			HandleMouseEvent();
			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, _overlayConfig.Opacity / 100f);
			_textureHandler.Render();
			ImGui.PopStyleVar();
		}
		else
		{
			ImGui.TextWrapped(_textureRenderException?.Message ?? "Loading Ember…");
		}

		ImGui.End();
	}

	private ImGuiWindowFlags GetWindowFlags()
	{
		ImGuiWindowFlags flags = ImGuiWindowFlags.None
		                         | ImGuiWindowFlags.NoTitleBar
		                         | ImGuiWindowFlags.NoCollapse
		                         | ImGuiWindowFlags.NoScrollbar
		                         | ImGuiWindowFlags.NoScrollWithMouse
		                         | ImGuiWindowFlags.NoBringToFrontOnFocus
		                         | ImGuiWindowFlags.NoFocusOnAppearing;

		// A locked overlay has no frame or resize grip.
		bool locked = _overlayConfig.Locked;

		if (locked)
		{
			flags |= ImGuiWindowFlags.None
			         | ImGuiWindowFlags.NoMove
			         | ImGuiWindowFlags.NoResize
			         | ImGuiWindowFlags.NoBackground;
		}

		if (!_captureCursor && locked)
		{
			flags |= ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoNav;
		}

		// don't think user wants a background when they decrease opacity
		if (_overlayConfig.Opacity < 100f)
			flags |= ImGuiWindowFlags.NoBackground;

		return flags;
	}

	public void SetTexture(IntPtr handle)
	{
		_resizing = false;
		_hasRenderError = false;

		SharedTextureHandler? oldTextureHandler = _textureHandler;
		try
		{
			_textureHandler = new SharedTextureHandler(handle);
		}
		catch (Exception e) { _textureHandler = null; _textureRenderException = e; }

		if (oldTextureHandler != null) { oldTextureHandler.Dispose(); }
	}

	private void HandleMouseEvent()
	{
		// Render proc won't be ready on first boot
		// Totally skip mouse handling for click through overlays, as well
		if (_renderProcess == null) { return; }

		ImGuiIOPtr io = ImGui.GetIO();
		Vector2 windowPos = ImGui.GetWindowPos();
		Vector2 mousePos = io.MousePos - windowPos - ImGui.GetWindowContentRegionMin();

		// Generally we want to use IsWindowHovered for hit checking, as it takes z-stacking into account -
		// but when cursor isn't being actively captured, imgui will always return false - so fall back
		// so a slightly more naive hover check, just to maintain a bit of flood prevention.
		// TODO: Need to test how this will handle overlaps... fully transparent _shouldn't_ be accepting
		//       clicks so shouuulllddd beee fineee???
		bool hovered = _captureCursor
			? ImGui.IsWindowHovered()
			: ImGui.IsMouseHoveringRect(windowPos, windowPos + ImGui.GetWindowSize());

		// manage focus
		MouseButton down = EncodeMouseButtons(io.MouseClicked);
		MouseButton double_ = EncodeMouseButtons(io.MouseDoubleClicked);
		MouseButton up = EncodeMouseButtons(io.MouseReleased);
		float wheelX = io.MouseWheelH;
		float wheelY = io.MouseWheel;
		

		// If the cursor is outside the window, send a final mouse leave then noop
		if (!hovered)
		{
			if (_mouseInWindow)
			{
				_mouseInWindow = false;
				_ = _renderProcess.Rpc?.MouseButton(new MouseButtonMessage() {Guid = RenderGuid.ToByteArray(), X = (int)mousePos.X, Y = (int)mousePos.Y, Leaving = true});
			}

			return;
		}

		_mouseInWindow = true;

		ImGui.SetMouseCursor(_cursor);

		// If the event boils down to no change, bail before sending
		if (io.MouseDelta == Vector2.Zero && down == MouseButton.None && double_ == MouseButton.None && up == MouseButton.None && wheelX == 0 && wheelY == 0)
		{
			return;
		}

		InputModifier modifier = InputModifier.None;
		if (io.KeyShift) { modifier |= InputModifier.Shift; }

		if (io.KeyCtrl) { modifier |= InputModifier.Control; }

		if (io.KeyAlt) { modifier |= InputModifier.Alt; }

		// TODO: Either this or the entire handler function should be asynchronous so we're not blocking the entire draw thread
		_ = _renderProcess.Rpc?.MouseButton(new MouseButtonMessage()
		{
			Guid = RenderGuid.ToByteArray(),
			X = mousePos.X,
			Y = mousePos.Y,
			Down = down,
			Double = double_,
			Up = up,
			WheelX = wheelX,
			WheelY = wheelY,
			Modifier = modifier
		});
	}

	private void HandleWindowSize()
	{
		Vector2 currentSize = ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin();
		if (currentSize.X < 1 || currentSize.Y < 1 || currentSize == _size || _resizing) { return; }

		if (_size == Vector2.Zero)
		{
			_ = _renderProcess.Rpc?.NewOverlay(new NewOverlayMessage()
			{
				Guid = RenderGuid.ToByteArray(),
				Id = "Ember",
				Url = EmberUrl,
				Width = (int)currentSize.X,
				Height = (int)currentSize.Y,
				Zoom = _overlayConfig.Zoom,
				Framerate = 60,
				Muted = false,
				CustomCss = ""
			});
		}
		else
		{
			_ = _renderProcess.Rpc?.ResizeOverlay(RenderGuid, (int)currentSize.X, (int)currentSize.Y);
		}

		_resizing = true;
		_size = currentSize;
	}

	#region serde

	private MouseButton EncodeMouseButtons(Span<bool> buttons)
	{
		MouseButton result = MouseButton.None;
		if (buttons[0]) { result |= MouseButton.Primary; }

		if (buttons[1]) { result |= MouseButton.Secondary; }

		if (buttons[2]) { result |= MouseButton.Tertiary; }

		if (buttons[3]) { result |= MouseButton.Fourth; }

		if (buttons[4]) { result |= MouseButton.Fifth; }

		return result;
	}

	private ImGuiMouseCursor DecodeCursor(Cursor cursor)
	{
		// ngl kinda disappointed at the lack of options here
		switch (cursor)
		{
			case Cursor.Default: return ImGuiMouseCursor.Arrow;
			case Cursor.None: return ImGuiMouseCursor.None;
			case Cursor.Pointer: return ImGuiMouseCursor.Hand;

			case Cursor.Text:
			case Cursor.VerticalText:
				return ImGuiMouseCursor.TextInput;

			case Cursor.NResize:
			case Cursor.SResize:
			case Cursor.NsResize:
				return ImGuiMouseCursor.ResizeNs;

			case Cursor.EResize:
			case Cursor.WResize:
			case Cursor.EwResize:
				return ImGuiMouseCursor.ResizeEw;

			case Cursor.NeResize:
			case Cursor.SwResize:
			case Cursor.NeswResize:
				return ImGuiMouseCursor.ResizeNesw;

			case Cursor.NwResize:
			case Cursor.SeResize:
			case Cursor.NwseResize:
				return ImGuiMouseCursor.ResizeNwse;
		}

		return ImGuiMouseCursor.Arrow;
	}

	private bool HiddenByCombatFlags()
	{
		if (!_overlayConfig.HideOutOfCombat)
		{
			return false;
		}

		if (Services.ObjectTable.LocalPlayer == null)
		{
			return true;
		}

		if (Services.ObjectTable.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat))
		{
			_timeLastInCombat = DateTimeOffset.Now.ToUnixTimeMilliseconds();
			return false;
		}

		if (!Services.ObjectTable.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat) && _overlayConfig.HideDelay > 0)
		{
			return DateTimeOffset.Now.ToUnixTimeMilliseconds() >= _timeLastInCombat + (_overlayConfig.HideDelay * 1000L);
		}

		return true;
	}

	#endregion
}