﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Results
{
    public class Result<T>
    {
        public bool IsSuccess { get; init; }
        public T? Value { get; init; }
        public string? Error { get; init; }

        public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
        public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
    }
}
