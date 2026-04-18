// <copyright file="IVramLayoutProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Resolves the <see cref="IVramLayout"/> for the current host GPU. The
/// default DXGI implementation lives in
/// <c>Ouroboros.Tensor.Configuration.DxgiVramLayoutProvider</c>; this
/// interface is the seam so tests and alternate hosts can substitute a
/// deterministic layout.
/// </summary>
public interface IVramLayoutProvider
{
    /// <summary>
    /// Returns the resolved layout. Implementations MUST NOT throw on DXGI
    /// failure — fall back to the most conservative preset (typically
    /// <c>Generic_8GB</c>) and log a single warning instead.
    /// </summary>
    /// <param name="configuration">
    /// Application configuration. The canonical override key is
    /// <c>Avatar:VramLayoutOverride</c>; when set to a known preset id the
    /// provider MUST return that preset unconditionally.
    /// </param>
    /// <returns>An immutable <see cref="IVramLayout"/> for this session.</returns>
    IVramLayout Resolve(IConfiguration configuration);
}
