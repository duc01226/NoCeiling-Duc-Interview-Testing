# Platform Document

This folder is to place building blocks/libraries projects in microservices architecture.

Buiding blocks like platform (abstraction), infrastructures or common utilities, things that could be re-used by services.

It should not contain any businesses or logics related to any domains, so that it could be re-used by any projects.

Examples:

- Utils: StringUtil, DateUtil, etc..
- Extensions: StringExtension, ListExtension, etc...
- EventBus
- Third-party services like: E-mail sender, Logging, etc ...

## Why do we build this ?

The purpose is by using those platform libraries and building blocks, we can standardlize and reuse code for all microservices in this project or any other projects.
