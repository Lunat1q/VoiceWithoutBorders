using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceWithoutBorders.Infrastructure.Network;

namespace VoiceWithoutBorders.Features.Main;

// Find on the Connection tab: looks for the app on other computers with the same port and key.
public sealed partial class MainViewModel
{
    private readonly ILanDiscovery _discovery;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPeersCommand))]
    private bool _isSearching;

    [ObservableProperty] private string _searchStatus = "";

    public ObservableCollection<DiscoveredPeer> FoundPeers { get; } = [];

    private bool CanFindPeers => !IsSearching;

    [RelayCommand(CanExecute = nameof(CanFindPeers))]
    private async Task FindPeersAsync()
    {
        FoundPeers.Clear();
        if (SharedKey.Length < PacketCodec.MinSharedKeyLength)
        {
            SearchStatus = $"Enter the shared key first (at least {PacketCodec.MinSharedKeyLength} characters), the same as on the other computer.";
            return;
        }
        IsSearching = true;
        SearchStatus = "Looking for other computers…";
        try
        {
            var peers = await _discovery.SearchAsync();
            foreach (var peer in peers)
                FoundPeers.Add(peer);
            if (peers.Count == 1)
                UsePeer(peers[0]);
            SearchStatus = peers.Count switch
            {
                0 => $"No computer answered. Open the app there with UDP port {Port} and this key, and allow it through Windows Firewall.",
                1 => $"Found {peers[0]}; its address is filled in above.",
                _ => "Found several computers: pick one.",
            };
        }
        catch (Exception ex)
        {
            SearchStatus = $"Could not search: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>The address, not the name: it is used as is, with no lookup that could resolve to the wrong adapter.</summary>
    [RelayCommand]
    private void UsePeer(DiscoveredPeer peer) => PeerHost = peer.Address.ToString();

    partial void OnPortChanged(int value) => UpdateDiscovery();

    partial void OnSharedKeyChanged(string value) => UpdateDiscovery();

    /// <summary>Answer searches from other computers while idle; streaming needs the port itself.</summary>
    private void UpdateDiscovery()
    {
        if (_loading)
            return;
        if (!IsRunning && Port is >= 1 and <= 65535 && SharedKey.Length >= PacketCodec.MinSharedKeyLength)
            _discovery.Listen(Port, SharedKey);
        else
            _discovery.StopListening();
    }
}
