# TabletUILib
UI Library for A Township Tale
Hellooo, I saw people wanted to use my mod BetterCam for its UI code (thank you VMan for the original idea) so I'm making it easier than that!
Instead of getting AI to look at the UI code you can make it easy on yourself and use this library.

# Install
Put TabletUILib.dll in your Mods folder

# Usage
Add a reference to TabletUILib.dll in your project and add a dependency for the lib
```csharp
[assembly: MelonAdditionalDependencies("TabletUILib")]
```

AddBubble makes the bubble on the wheel .App, .Toggle and .Action add sub bubbles
```csharp
TabletUI.AddBubble("my mod", "MINE")
    .App("settings", "SET", Home)        // opens the tablet ui
    .Toggle("on/off", "ON", enabled)     // on/off switch
    .Action("hello", "HI", () => MelonLogger.Msg("hi"));  // runs code
```

A page is the ui on the tablet
```csharp
static Page Home()
{
    return new Page("MY MOD", "a line under the title")
        .Toggle("enabled", enabled)
        .Slider("speed", speed, 1f, 20f, 0.5f)
        .Button("say hi", () => MelonLogger.Msg("hi"))
        .Text("just text")
        .Link("more", MorePage);
}
```
enabled and speed are MelonPreferences entries, they save by themselves

# Server stuff
The lib works client only you don't need it on the server.
.Action runs on the players game, so for anything the server has to do use .ServerAction instead

.ServerAction runs your code on the server with whoever pressed it
```csharp
TabletUI.AddBubble("admin", "ADM")
    .ServerAction("heal me", "HEAL", player => { /* runs on the server */ })
    .ServerAction("kick all", "KICK", player => { /* only admins */ }, "admin");
```
- TabletUILib and your mod both have to be on the server
- Add the same bubble on the server too, thats how it knows what code to run
- It won't show up on servers that don't have that button
- The role at the end is optional, it uses TavernLib roles and the server ignores anyone without it
- It doesn't do cooldowns or anything like that, check that stuff yourself in your code

If you send stuff to the server yourself instead, add .NeedsServer() so the bubble won't show up on servers that don't have your mod
```csharp
TabletUI.AddBubble("teleport", "TP")
    .NeedsServer()                       // or .NeedsServer("ModName")
    .Action("home", "HOME", () => { /* send to your server side */ });
```
This one needs TabletUILib on the server too, thats how it knows what mods the server has.
you can also check it yourself with ServerMods.Has("ModName"), ModName is the name in your MelonInfo

# Notes
- The tablet shows 4 rows at a time, more rows get page arrows
- A bubble with no sub bubbles wont show up
- Bubble names have to be different from other bubbles
