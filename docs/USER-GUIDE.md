# Mochi desktop companion

Open **Launch Desktop Companion.cmd** in the project folder. The compiled app is `app/MochiDuo.exe`; keep that folder's files together. This build uses the installed .NET 10 Desktop Runtime.

- **Settings:** choose one or two characters, enter Google/Fish keys and optional fallback keys, then click the pinned **Save settings** button. Existing encrypted keys and history are reused.
- **Companions:** select your first and second companions and click **Use selected companions**. Solo mode uses the first companion. Choose Short, Normal, or Long for each reply's length.
- **Create companion:** enter a name, personality, optional Fish voice ID, and main image. Optional sad, pouting, and angry images fall back to the main image. Transparent PNGs retain their transparency; backgrounds in opaque artwork are not automatically removed. Save, then select your new companion. Editing a built-in companion creates a separate remix.
- **Chat:** choose output language, enable or disable speech, and enter a message or topic. Duration controls the number of duo turns; Ultimate continues until Stop. API usage continues while Ultimate is running.
- **Desktop:** click Show on desktop to minimize the dashboard. Drag a companion to move it. Double-click the character or click its speech bubble to reopen the dashboard.
- **Taskbar life:** enable it in Settings and save to use smaller sprites that wander, bounce, and rest above the primary monitor's taskbar. Right-click a character to pause/resume walking. These animations move the supplied still artwork; they are not articulated walking animations.
- **Always on top:** enable and save to keep both companion windows visible above other apps. New profiles default to on; existing preferences are preserved.
- **Occasional AI chatter:** an opt-in setting for one short primary-companion reply about every three minutes while the dashboard is minimized and the message box is empty. It uses your selected output language and speech setting, consumes provider quota, and pauses while a conversation is running. Stop cancels the current reply; switch the setting off to disable future chatter.
- **History:** the last 500 messages are stored encrypted for your Windows account.

Artwork and custom profiles are copied into `%LOCALAPPDATA%\MochiDuo\companions`. API keys are stored separately using Windows encryption. Custom companion names and personalities are sent to Gemini when chatting; reply text and the selected voice ID are sent to Fish when speech is enabled.

With speech enabled, text is revealed when the audio player opens the matching audio and starts playback. Duo prepares one complete reply ahead with the preceding dialogue in context. Stop cancels pending generation. Actual sound timing depends on the Windows audio device.
