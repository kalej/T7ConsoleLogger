# T7ConsoleLogger - fast logging for Trionic 7

This solution is a proof-of-concept program that aims to make Trionic 7 logging fast.
It uses KWP-over-CAN diagnostic protocol and heavily relies on the concept of Dynamically Defined Identifiers.

Trionic 7 supports KWP DynamicallyDefineLocalIdentifier request and allows defining up to 60 variables with total length of 248 (? needs revision) bytes.
Trionic 7 allows defining new identifiers by:
 - Local identifier
 - Symbol number
 - Address and length
Trionic 7 supports KWP ReadDataByLocalId request and returns all defined variables in one response.

The benefits of this method are:
 - Very fast logging (up to 32FPS for a set of 17 variables with total length of 33 bytes stable, 71FPS for the same set in berserk mode).
 - Only one short KWP request (that will fit into one CAN message) to read the values of all defined variables.
 - Data consistency, because values are first read into reply buffer and then sent over CAN.
 - Less read-write overhead as only one KWP reply is sent (so only one KWP header is required).
 - Native timing by adding ECMStat.msCounter to the list of logged variables.

Other features:
 - CAN monitor that observes CAN activity from other modules and warns user if some messages are coming with extended period.
 - Logging to SQLite database that is faster than writing logs to text file. Also, brings more post-processing options, e.g., Pandas and others.
 - Improved KWP-over-CAN request sending process for long requests (that does not matter much as requests for getting data are short).

Disadvantages of the current solution:
 - Not user-friendly. At all. Everything should be configured in XML file.
 - Only ComdiAdapter supported.
 - No export from SQLite database provided.

Future improvements:
 - [ ] Get security access to define variables by address.
 - [x] Support variables that are arrays (e.g. ignition offsets and others).
 - [ ] ~~User-friendly interface~~