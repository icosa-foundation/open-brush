# unity-websocket-server
A simple, zero-dependency WebSocket server for Unity.

## Usage
First import the package and set up the scene:
* Download the UnityPackage from the [Releases page](https://github.com/shaunabanana/unity-websocket-server/releases). Import.
* Create an empty GameObject, assign the script `WebSocketServer.cs`.
  * Alternatively, if you're using inheritance, assign your own script.
* Input the IP and port of the server.

### In Inspector GUI
The easiest way is to use the inspector. Add callbacks to the `onOpen`, `onMessage`, or `onClose` UnityEvent.

### Using class inheritance
Alternatively, you can inherit from `WebSocketServer`. The following code shows you how:
```csharp
using WebSocketServer;

public class MyWebSocketServer: WebSocketServer {

  override public void OnOpen(WebSocketConnection connection) {
    // Here, (string)connection.id gives you a unique ID to identify the client.
    Debug.Log(connection.id);
  }
  
  override public void OnMessage(WebSocketMessage message) {
    // (WebSocketConnection)message.connection gives you the connection that send the message.
    // (string)message.id gives you a unique ID for the message.
    // (string)message.data gives you the message content.
    Debug.Log(message.connection.id);
    Debug.Log(message.id);
    Debug.Log(message.data);
  }
  
  override public void OnClose(WebSocketConnection connection) {
    // Here is the same as OnOpen
    Debug.Log(connection.id);
  }

}

```

## Tested on
> If you find this script works/doesn't work for you, please let me know by creating an [issue](https://github.com/shaunabanana/unity-websocket-server/issues) or emailing me at shengchenzhang1207@gmail.com.
> Also, if there's a feature you want added, there's a template for that as well in the issues.

**Unity versions**
* 2020.3

**System versions**
* macOS Big Sur (11.2.3)
