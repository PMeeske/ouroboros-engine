// <copyright file="GlobalUsings.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

// System namespaces not covered by SDK implicit usings
global using System.Collections.Immutable;

// Core sub-namespaces
global using Ouroboros.Core.Synthesis;

// Pipeline sub-namespaces
global using Ouroboros.Pipeline.Ingestion;
global using Ouroboros.Pipeline.Memory;

// LangChain libraries (used extensively across ingestion/branch/replay tests)
global using LangChain.Databases;
global using LangChain.DocumentLoaders;
global using LangChain.Splitters.Text;

// Third-party test libraries
global using Moq;
