# Arquivos deste passe

Modificados: `.gitignore`, `Heartbeat.csproj`, `Main.tscn`, `README.md`, `Scripts/Main.cs`, `Scripts/Models.cs`, `Scripts/Repositories.cs`, `Scripts/GameplayServices.cs`, `Scripts/Systems.cs`, `Scripts/UI/MainMenuController.cs`.

Criados: `Scenes/World/World.tscn`, `Scenes/SmokeTest.tscn`, `Scripts/World/WorldController.cs`, `Scripts/World/PlayerController.cs`, `Scripts/World/NpcActor.cs`, `Scripts/Characters/PortraitCache.cs`, `Scripts/Dialogue/DialogueController.cs`, `Scripts/UI/Ui.cs`, `Scripts/UI/CharacterCreatorController.cs`, `Scripts/UI/AuxiliaryScreens.cs`, `Scripts/Testing/SmokeTest.cs`, `Scripts/Testing/ProviderChecks.cs` e este registro. As capturas temporárias de validação foram removidas depois dos testes.

O Godot pode gerar arquivos `.uid` e `.import` junto aos recursos. `project.godot` já apontava para o menu e foi preservado.

O projeto C# agora inclui somente `Scripts/**/*.cs`: uma cópia exportada do projeto havia sido extraída dentro do workspace e provocava compilação duplicada. A cópia não foi apagada.
