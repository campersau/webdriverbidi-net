// <copyright file="EvaluateResultException.cs" company="WebDriverBiDi.NET Committers">
// Copyright (c) WebDriverBiDi.NET Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace WebDriverBiDi.Script;

using Newtonsoft.Json;

/// <summary>
/// Object representing the evaluation of a script that throws an exception.
/// </summary>
public class EvaluateResultException : EvaluateResult
{
    private ExceptionDetails result = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluateResultException"/> class.
    /// </summary>
    [JsonConstructor]
    internal EvaluateResultException()
        : base()
    {
    }

    /// <summary>
    /// Gets the exception details of the script evaluation.
    /// </summary>
    [JsonProperty("exceptionDetails")]
    public ExceptionDetails ExceptionDetails { get => this.result; internal set => this.result = value; }
}
