# Overview

This document outlines the coding guidelines and structure, and what patterns to prefer to stick to.

Overall we tend to use this website as a source of some of our patterns, plus industry standard approaches: https://gameprogrammingpatterns.com

- **Single Responsibility:** Each class and file should be scoped to one confined area that is easily unit testable. 
- **Separation of Concerns:** We should not mix repsonsibkilities into one file / class. Examples include, having input, graphics, and sound details all in one place. The exceptions to this is when we have higher level classes that are using abstractions from lower level to create a game mechanic. 
- **Component Pattern:** We should use the component pattern to encapsulate business logic into a single unit, for example InputComponent, PlayerInputComponent, GraphicsComponent, etc.
- We should use composition as the default, and only use inheritance in documented circumstances with a valid reason.https://gameprogrammingpatterns.com/component.html
- **Command Pattern:** We should use the command pattern (where sensible) to solve the issue of having multiple different behaviours, we encapsulate behaviour into commands that can be swapped and changed during runtime, but adhere to a common interface. https://gameprogrammingpatterns.com/command.html
- **Observer Pattern:** We should use the observer pattern when we have events in our system that needs lots of different systems to act upon the event: https://gameprogrammingpatterns.com/observer.html
- **High Cohesion:** We should keep related code together as much as possible. However we should not try to force a high cohesion if it does not make sense. E.g. for any shared functionality that does not cleaner slice into a contained vertical slice.
- **Game Project Structure:**
    - We should keep Vocaterra.Game.Core project containing all the of the game's system and mechanics logic that is not tied to the underlying graphics framework (in this case MonoGame). This is to encourage easier unit testing, and to avoid tangling framework code, with raw domain logic of our game. Examples include alogirthms, player inventory management, physics simulation, npc interaction logic, language processing and decision making, etc.
    -  We should keep Vocaterra.Game project to containing any code that is grahpcis related, UI related, and needs the explict reference of the MonoGame framework. Examples include calling Monogame functions for graphics, rendering, input management, and user interface elements.
- **Testing:** We should separate testing into 2 areas: Unit and Component testing. Each testing project lives in its own .csproj, *.Unit.Tests and *.Component.Tests
    - Unit testing involves testing a singular 'unit' e.g. a class or a function to ensure it adheres to its goal. Mocking can be used here, but prefer concrete implementations were possible.
        - Testing folder structure should always match the structure of the source code. For example if a class is in Utility/, then the test should also be in the same folder Utility, but in the test project. So the folder structure always matches. 
    - Component testing involves testing 2 or more components, e.g. a UI element, and the corresponding domain logic, for example the Dialogue Panel in conjunctiobn with the language and input processing logic.
    - All tests should follow the naming convention of: Given_When_Then.

