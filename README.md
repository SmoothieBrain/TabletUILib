# TabletUILib
UI Library for A Township Tale
Hellooo, I saw people wanted to use my mod BetterCam for its UI code (thank you VMan for the original idea) so I'm making it easier than that!
Instead of getting AI to look at the UI code you can make it easy on yourself and use this library.

# Usage
Reference TabletUILib.dll to your project and add a dependency for the lib,
```csharp
   [assembly: MelonAdditionalDependencies("TabletUILib")]
   ```

Here is a quick example on how to make bubbles, sub bubbles, and UI
```csharp
TabletUI.AddBubble("name", "whatshowsontheicon")
```
The .App and .Toggle add sub bubbles.
```csharp
TabletUI.AddBubble("better cam", "CAM")   // makes the bubble on the wheel
    .App("settings", "SET", Home)         //button 1 opens the tablet ui
    .Toggle("effects", "FX", on);         //button 2 on/off switch
```


