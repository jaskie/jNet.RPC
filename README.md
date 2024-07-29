# jNet.RPC
This project was created as easy to use Remote Procedure Call (RPC) in client-server object environment.

There are plenty of communication means and frameworks, but almost all of them requires us to communicate with the server like through keyhole. This library is response to my personal need to have a tool that allow me to have, at client, a reflection of all obtained server objects. And use them in a way as if they were local, using the network.
So it's possible to:
 - transfer an object from server to client,
 - execute its methods,
 - get and set its properties,
 - subscribe to its events.
 
Sounds pretty easy?
Under the hood it's little less, but still relatively simple.
 
 # History
The framework was created as part of [TVPlay](https://github.com/jaskie/PlayoutAutomation) project, a TV play-out automation system. Therefore it does not have a big usage base. But its versatility made me to extract it to separate library, to use it in other projects as well. 

# Limitations and dependencies
Currently it works over TCP connections only (however initially was using WebSockets). It heavily relies on Newtonsoft Json and uses NLog.

# Usage
To use it, it is reqired that all your persistent server classes were based on `ServerObjectBase`. I know, it's huge claim, but the class makes lot of work for us. It, for example, can notify all clients about changing its property values (using built-in `INotifyPropertyChanged` interface). Such a server class should also implement its interface (i.e. used by application GUI). The same interface will be implemented on client side as proxy class.

Corresponding client proxy classes are auto-generated using MSIL, but they can also be written manually when more control of its behavior is needed. 

We'll need also a server `ServerHost` and client `RemoteCLient` classes to maintain communication. And mapping between server and client classes, in a class that implements `Newtonsoft.Json.Serialization.ISerializationBinder`. That's all.

A simple example of usage is included in the project.
