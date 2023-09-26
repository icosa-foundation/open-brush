

using System;

namespace OpenBrush.Multiplayer
{
    public interface IConnectionHandler
    {
        bool Connect();
        bool Disconnect(bool force = false);
    }
}
