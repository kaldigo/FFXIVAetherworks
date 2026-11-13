// Adapted from Browsingway 1.7.2, GPL-3.0. See vendor/Browsingway.
﻿using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Aetherworks.Ember;

public class Services
{
	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static ICommandManager CommandManager { get; private set; } = null!;

	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static IChatGui Chat { get; private set; } = null!;

	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static IPluginLog PluginLog { get; private set; } = null!;

	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static ITextureProvider TextureProvider { get; private set; } = null!;

	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static IFramework Framework { get; private set; } = null!;

	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static IClientState ClientState { get; set; } = null!;

	[PluginService]
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
	public static IObjectTable ObjectTable { get; set; } = null!;
}