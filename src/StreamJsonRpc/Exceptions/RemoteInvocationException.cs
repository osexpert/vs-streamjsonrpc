﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace StreamJsonRpc
{
    using System;
    using Newtonsoft.Json.Linq;
    using StreamJsonRpc.Protocol;

    /// <summary>
    /// Remote RPC exception that indicates that the server target method threw an exception.
    /// </summary>
    /// <remarks>
    /// The details of the target method exception can be found on the <see cref="ErrorCode"/> and <see cref="ErrorData"/> properties.
    /// </remarks>
    [System.Serializable]
    public class RemoteInvocationException : RemoteRpcException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteInvocationException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="errorCode">The value of the error.code field in the response.</param>
        /// <param name="errorData">The value of the error.data field in the response.</param>
        public RemoteInvocationException(string? message, int errorCode, object? errorData)
            : base(message)
        {
            this.ErrorCode = errorCode;
            this.ErrorData = errorData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteInvocationException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="errorCode">The value of the error.code field in the response.</param>
        /// <param name="errorData">The value of the error.data field in the response.</param>
        /// <param name="deserializedErrorData">The value of the error.data field in the response, deserialized according to <see cref="JsonRpc.GetErrorDetailsDataType(JsonRpcError)"/>.</param>
        public RemoteInvocationException(string? message, int errorCode, object? errorData, object? deserializedErrorData)
            : base(message)
        {
            this.ErrorCode = errorCode;
            this.ErrorData = errorData;
            this.DeserializedErrorData = deserializedErrorData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteInvocationException"/> class.
        /// </summary>
        /// <param name="info">Serialization info.</param>
        /// <param name="context">Streaming context.</param>
        protected RemoteInvocationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets the value of the <c>error.code</c> field in the response.
        /// </summary>
        /// <value>
        /// The value may be any integer.
        /// The value may be <see cref="JsonRpcErrorCode.InvocationError"/>, which is a general value used for exceptions thrown on the server when the server does not give an app-specific error code.
        /// </value>
        public int ErrorCode { get; }

        /// <summary>
        /// Gets the <c>error.data</c> value in the error response, if one was provided.
        /// </summary>
        /// <remarks>
        /// Depending on the <see cref="IJsonRpcMessageFormatter"/> used, the value of this property, if any,
        /// may be a <see cref="JToken"/> or a deserialized object.
        /// If a deserialized object, the type of this object is determined by <see cref="JsonRpc.GetErrorDetailsDataType(JsonRpcError)"/>.
        /// The default implementation of this method produces a <see cref="CommonErrorData"/> object.
        /// </remarks>
        public object? ErrorData { get; }

        /// <summary>
        /// Gets the <c>error.data</c> value in the error response, if one was provided.
        /// </summary>
        /// <remarks>
        /// The type of this object is determined by <see cref="JsonRpc.GetErrorDetailsDataType(JsonRpcError)"/>.
        /// The default implementation of this method produces a <see cref="CommonErrorData"/> object.
        /// </remarks>
        public object? DeserializedErrorData { get; }
    }
}
