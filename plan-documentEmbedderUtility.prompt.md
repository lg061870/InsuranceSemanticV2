## Plan: Document Embedder Utility for the Add‑in

Add a “Document Embedder” feature to the ConversaCore Topic Tool that lets the domain dev declare document collections (per-topic or global), and have the add‑in generate/maintain ingestion services and wiring, all on top of the existing ConversaCore embedding/vector APIs.

### Steps

1. Model document collections and scopes  
   - Represent each collection with: logical name, folder(s) under a `Documents/` tree, file patterns, scope (topic-specific vs global), and basic options (e.g., chunk size, types).  
   - Persist this as a tool-owned config (e.g., JSON in the project), not in ConversaCore, and link topic-scoped collections to specific topic IDs/classes.

2. Build a “Document Collections” tool window in the add‑in  
   - Allow creating/editing collections: choose scope (Global vs Topic), pick folders, patterns, and associate with one or more topics.  
   - Validate configuration (missing directories, duplicate collection names, unsupported types) and surface issues as warnings in the UI.

3. Generate ingestion services aligned with existing patterns  
   - For each collection (or group of related collections), generate/maintain a domain-level ingestion service (e.g., `XyzDocumentsEmbeddingService`) that:  
     - Injects `IEmbeddingGenerator` and `IVectorDatabaseService`.  
     - Reads the tool’s config, scans the configured folders, and upserts embeddings into a derived vector collection name.  
   - Generate a central “DocumentIngestionCoordinator” that knows all collections and can run: one collection, all for a topic, or all global.

4. Wire services into startup (Program.cs)  
   - Have the add‑in maintain a small, well-delimited region in `Program.cs` (or a dedicated registration class) where it:  
     - Registers each generated ingestion service and the coordinator into DI.  
     - Binds the document‑collection config.  
   - Optionally insert a commented sample that calls `SyncAsync` / “RunAllCollectionsAsync” at startup, which the domain dev can enable or disable.

5. Provide design-time ingestion triggers from VS  
   - Add commands in the add‑in (e.g., context menu or toolbar) to:  
     - “Dry run” a collection: show which files would be ingested and into which vector collection.  
     - “Ingest now”: trigger the coordinator for a selected collection, topic, or all.  
   - Implement triggers by either:  
     - Launching a small helper console entry point that uses the same DI + coordinator, or  
     - Calling a minimal API endpoint in the running web app (if available) and reporting progress back in the tool window.

### Further Considerations

1. Naming and mapping: standardize a vector collection naming convention (e.g., `<AppPrefix>_<TopicIdOrGlobal>_<CollectionName>`) so topics and the tool both refer to collections consistently.  
2. Safety and regeneration: treat all ingestion services as generated files; re‑generate from the config without touching developer-authored code, and label them clearly as tool-owned.  
3. Topic integration: in the topic designer, surface available collections so the domain dev can choose which knowledge bases a topic should use; the tool then ensures appropriate collection names are available for activities like `SemanticResponseActivity`.  
