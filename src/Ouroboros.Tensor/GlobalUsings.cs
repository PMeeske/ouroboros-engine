// <copyright file="GlobalUsings.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

// System imports
global using System;
global using System.Buffers;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Diagnostics;
global using System.Diagnostics.Metrics;
global using System.Linq;
global using System.Numerics.Tensors;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Threading;
global using System.Threading.Tasks;

// Logging
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;

// Foundation monads and arrows
global using Ouroboros.Core.Kleisli;
global using Ouroboros.Abstractions.Monads;
global using Ouroboros.Core.Monads;
global using Ouroboros.Core.Steps;

// Tensor sub-namespaces (all layers auto-visible within the project)
global using Ouroboros.Tensor.Abstractions;
global using Ouroboros.Tensor.Adapters;
global using Ouroboros.Tensor.Backends;
global using Ouroboros.Tensor.Configuration;
global using Ouroboros.Tensor.Decorators;
global using Ouroboros.Tensor.Extensions;
global using Ouroboros.Tensor.Lazy;
global using Ouroboros.Tensor.Memory;
global using Ouroboros.Tensor.Models;
global using Ouroboros.Tensor.Orchestration;
global using Ouroboros.Tensor.Pipeline;
