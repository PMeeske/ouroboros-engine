// <copyright file="GlobalUsings.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

// System namespaces not covered by SDK implicit usings
global using System.Collections.Immutable;

// Core sub-namespaces
global using Ouroboros.Core.Synthesis;

// Pipeline sub-namespaces
global using Ouroboros.Pipeline.Ingestion;
global using Ouroboros.Pipeline.Memory;

// Domain libraries (used extensively across ingestion/branch/replay tests)
global using Ouroboros.Domain.DocumentLoaders;
global using Ouroboros.Domain.Vectors;
global using Ouroboros.Domain.TextSplitters;

// Third-party test libraries
global using Moq;
