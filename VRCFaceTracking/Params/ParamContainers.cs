﻿using System;
using System.Collections.Generic;
using System.Linq;
using ParamLib;
using ViveSR.anipal.Lip;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCFaceTracking.Params
{
    public class FloatParameter : FloatBaseParam, IParameter
    {
        public FloatParameter(Func<EyeTrackingData, Dictionary<LipShape_v2, float>, float?> getValueFunc, string paramName, bool wantsPriority = false)
            : base(paramName, wantsPriority) =>
            UnifiedTrackingData.OnUnifiedParamsUpdated += (eye, lipFloats, lip) =>
            {
                if (!UnifiedLibManager.EyeEnabled && !UnifiedLibManager.LipEnabled) return;
                var value = getValueFunc.Invoke(eye, lip);
                if (value.HasValue)
                    ParamValue = value.Value;
            };

        public string[] GetName() => new[] {ParamName};
    }

    public class XYParameter : XYParam, IParameter
    {
        public XYParameter(Func<EyeTrackingData, Dictionary<LipShape_v2, float>, Vector2?> getValueFunc, string xParamName, string yParamName)
            : base(new FloatBaseParam(xParamName, true), new FloatBaseParam(yParamName, true))
        {
            UnifiedTrackingData.OnUnifiedParamsUpdated += (eye, lipFloats, lip) =>
            {
                if (!UnifiedLibManager.EyeEnabled && !UnifiedLibManager.LipEnabled) return;
                var value = getValueFunc.Invoke(eye, lip);
                if (value.HasValue)
                    ParamValue = value.Value;
            };
        }

        public XYParameter(Func<EyeTrackingData, Vector2> getValueFunc, string xParamName, string yParamName)
            : this((eye, lip) => getValueFunc.Invoke(eye), xParamName, yParamName)
        {
        }

        void IParameter.ResetParam() => ResetParams();
        public void ZeroParam() => ZeroParams();
        public string[] GetName() => new[] {X.ParamName, Y.ParamName};

    }

    public class BoolParameter : BoolBaseParam, IParameter
    {
        public BoolParameter(Func<EyeTrackingData, Dictionary<LipShape_v2, float>, bool?> getValueFunc, string paramName) : base(paramName) =>
            UnifiedTrackingData.OnUnifiedParamsUpdated += (eye, lipFloats, lip) =>
            {
                if (!UnifiedLibManager.EyeEnabled && !UnifiedLibManager.LipEnabled) return;
                var value = getValueFunc.Invoke(eye, lip);
                if (value.HasValue)
                    ParamValue = value.Value;
            };

        public BoolParameter(Func<EyeTrackingData, bool> getValueFunc, string paramName) : this(
            (eye, lip) => getValueFunc.Invoke(eye), paramName)
        {
        }

        public string[] GetName() => new [] {ParamName};
    }

    public class BinaryParameter : IParameter
    {
        private readonly List<BoolParameter> _params = new List<BoolParameter>();
        private readonly BoolParameter _negativeParam;
        private readonly string _paramName;
        private readonly Func<EyeTrackingData, Dictionary<LipShape_v2, float>, float?> _getValueFunc;

        /* Pretty complicated, but let me try to explain...
         * As with other ResetParam functions, the purpose of this function is to reset all the parameters.
         * Since we don't actually know what parameters we'll be needing for this new avatar, nor do we know if the parameters we currently have are valid
         * it's just easier to just reset everything.
         *
         * Step 1) Find all valid parameters on the new avatar that start with the name of this binary param, and end with a number.
         * 
         * Step 2) Find the binary steps for that number. That's the number of shifts we need to do. That number could be 8, and it's steps would be 3 as it's 3 steps away from zero in binary
         * This also makes sure the number is a valid base2-compatible number
         *
         * Step 3) Calculate the maximum possible value for the discovered binary steps, then subtract 1 since we count from 0.
         *
         * Step 4) Create each parameter literal that'll be responsible for actually changing parameters. It's output data will be multiplied by the highest possible
         * binary number since we can safely assume the highest possible input float will be 1.0. Then we bitwise shift by the binary steps discovered in step 2.
         * Finally, we use a combination of bitwise AND to get whether the designated index for this param is 1 or 0.
         */
        public void ResetParam()
        {
            _negativeParam.ResetParam();
            
            // Get all parameters starting with this parameter's name, and of type bool
            var boolParams = ParamLib.ParamLib.GetLocalParams().Where(p => p.valueType == VRCExpressionParameters.ValueType.Bool && p.name.StartsWith(_paramName));

            var paramsToCreate = new Dictionary<string, int>();
            foreach (var param in boolParams)
            {
                // Cut the parameter name to get the index
                if (!int.TryParse(param.name.Substring(_paramName.Length), out var index)) continue;
                // Get the shift steps
                var binaryIndex = GetBinarySteps(index);
                // If this index has a shift step, create the parameter
                if (binaryIndex.HasValue)
                    paramsToCreate.Add(param.name, binaryIndex.Value);
            }

            if (paramsToCreate.Count == 0) return;
            
            // Calculate the highest possible binary number
            var maxPossibleBinaryInt = Math.Pow(2, paramsToCreate.Values.Count);
            foreach (var param in paramsToCreate)
                _params.Add(new BoolParameter(
                    (eye, lip) =>
                    {
                        var valueRaw = _getValueFunc.Invoke(eye, lip);
                        if (!valueRaw.HasValue) return null;
                        // If the value is negative, make it positive
                        valueRaw = valueRaw > 1 ? 1 : valueRaw < -1 ? -1 : valueRaw;
                        if (_negativeParam.ParamIndex == null &&
                            valueRaw < 0) // If the negative parameter isn't set, cut the negative values
                            return null;
                        
                        // Ensure value going into the bitwise shifts is between 0 and 1
                        valueRaw = Math.Abs(valueRaw.Value);

                        var value = (int) (valueRaw * (maxPossibleBinaryInt - 1));
                        return ((value >> param.Value) & 1) == 1;
                    }, param.Key));
        }
        
        // This serves both as a test to make sure this index is in the binary sequence, but also returns how many bits we need to shift to find it
        private static int? GetBinarySteps(int index)
        {
            var currSeqItem = 1;
            for (var i = 0; i < index; i++)
            {
                if (currSeqItem == index)
                    return i;
                currSeqItem*=2;
            }
            return null;
        }

        public void ZeroParam()
        {
            _negativeParam.ZeroParam();
            foreach (var param in _params)
                param.ZeroParam();
            _params.Clear();
        }

        public string[] GetName() =>
            // If we have no parameters, return a single value array containing the paramName. If we have values, return the names of all the parameters
            _params.Count == 0 ? new[] {_paramName} : _params.Select(p => p.ParamName).ToArray();

        public BinaryParameter(Func<EyeTrackingData, Dictionary<LipShape_v2, float>, float?> getValueFunc, string paramName)
        {
            _paramName = paramName;
            _getValueFunc = getValueFunc;
            
            _negativeParam = new BoolParameter((eye, lip) =>
            {
                var valueRaw = _getValueFunc.Invoke(eye, lip);
                if (!valueRaw.HasValue) return null;
                return valueRaw < 0;
            }, _paramName + "Negative");
        }

        public BinaryParameter(Func<EyeTrackingData, float> getValueFunc, string paramName) : this((eye, lip) => getValueFunc.Invoke(eye), paramName)
        {
        }
    }

    // EverythingParam, or EpicParam. You choose!
    // Contains a bool, float and binary parameter, all in one class with IParameter implemented.
    public class EParam : IParameter
    {
        private readonly IParameter[] _parameter;

        public EParam(Func<EyeTrackingData, Dictionary<LipShape_v2, float>, float?> getValueFunc, string paramName, float minBoolThreshold = 0.5f)
        {
            var boolParam = new BoolParameter((eye, lip) => getValueFunc.Invoke(eye, lip) < minBoolThreshold, paramName);
            var floatParam = new FloatParameter(getValueFunc, paramName, true);
            var binaryParam = new BinaryParameter(getValueFunc, paramName);
            
            _parameter = new IParameter[] {boolParam, floatParam, binaryParam};
        }

        public EParam(Func<EyeTrackingData, float> getValueFunc, string paramName,
            float minBoolThreshold = 0.5f) : this((eye, lip) => getValueFunc.Invoke(eye), paramName, minBoolThreshold)
        {
        }

        public string[] GetName()
        {
            var names = new List<string>();
            foreach (var param in _parameter)
                names.AddRange(param.GetName());
            return names.ToArray();
        }

        public void ResetParam()
        {
            foreach (var param in _parameter)
                param.ResetParam();
        }

        public void ZeroParam()
        {
            foreach (var param in _parameter)
                param.ZeroParam();
        }
    }
}