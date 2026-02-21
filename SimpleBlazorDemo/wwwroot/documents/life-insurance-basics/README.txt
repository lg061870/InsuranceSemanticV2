Life Insurance Basics Educational Module
=======================================

This folder contains the source documents for the "life-insurance-basics"
educational module used by the FirstTimeVisitorTopic in SimpleBlazorDemo.

How it works
------------
- On application startup, the LifeInsuranceBasicsEmbeddingService scans this
  folder and processes all supported files (txt, md, pdf, etc.).
- Documents are chunked and embedded via ConversaCore's DocumentProcessingService
  and IVectorDatabaseService.
- The resulting chunks are stored in the vector collection:
    life_insurance_basics
- When a user in the FirstTimeVisitorTopic chooses the educational path and
  selects "Give me a quick overview", a SemanticResponseActivity queries that
  collection and generates an answer based primarily on these documents.

How to update content
---------------------
- Add or update files in this folder.
- Restart the SimpleBlazorDemo application so the startup sync can re-process
  the documents and refresh the vector collection.

Notes
-----
- Keep documents focused on general life insurance basics (e.g., term vs whole,
  why people buy life insurance, how much coverage might be needed, etc.).
- Avoid including sensitive or environment-specific data; this content is meant
  purely for educational explanations.
