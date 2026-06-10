using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.InferenceEngine.Samples.TTS.Assets;
using Unity.InferenceEngine.Samples.TTS.Utils;
using UnityEngine;

namespace Unity.InferenceEngine.Samples.TTS.Inference
{
    public class KokoroHandler: IDisposable
    {
        const string k_KokoroModelPath = "onnx/model";
        const string k_VoicesFolderPath = "Voices/";

        Model m_Model;
        Worker m_Worker;
        readonly BackendType m_BackendType;

        public KokoroHandler(BackendType backendType = BackendType.GPUCompute, bool lazyLoadModel = true)
        {
            m_BackendType = backendType;

            if (!lazyLoadModel)
                LoadModelIfMissing();
        }

        void LoadModelIfMissing()
        {
            if (m_Model != null && m_Worker != null)
                return;

            Debug.Log($"[KokoroHandler] Attempting to load model from Resources: 'Resources/{k_KokoroModelPath}'...");
            var modelAsset = Resources.Load<ModelAsset>(k_KokoroModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[KokoroHandler] FAILED TO LOAD MODEL at 'Resources/{k_KokoroModelPath}'. \n" +
                               "1. Ensure the file 'model.onnx' exists in a Resources folder.\n" +
                               "2. Ensure it is imported as a 'ModelAsset' (requires Unity Sentis package).\n" +
                               "3. If using Git LFS, ensure the file was correctly pulled (not just a pointer).");
                return;
            }

            try
            {
                Debug.Log("[KokoroHandler] Compiling Model...");
                m_Model = ModelLoader.Load(modelAsset);
                if (m_Model == null)
                {
                    Debug.LogError("[KokoroHandler] ModelLoader.Load returned null! The model file might be corrupted or incompatible.");
                    return;
                }

                Debug.Log($"[KokoroHandler] Creating Worker with Backend: {m_BackendType}...");
                m_Worker = new Worker(m_Model, m_BackendType);
                Debug.Log("[KokoroHandler] Model and Worker successfully initialized.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KokoroHandler] CRITICAL ERROR during model/worker initialization: {ex.Message}\n{ex.StackTrace}");
                if (m_BackendType != BackendType.CPU)
                {
                    Debug.LogWarning("[KokoroHandler] GPU backend may have failed. Consider switching KokoroManager backend to CPU in the Inspector.");
                }
                m_Model = null;
                m_Worker = null;
            }
        }

        public async Task<Tensor<float>> Execute(int[] inputIds, float speed, Voice voice)
        {
            // Add the pad ids
            var paddedInputIds = new int[inputIds.Length + 2];
            paddedInputIds[0] = 0;
            Array.Copy(inputIds, 0, paddedInputIds, 1, inputIds.Length);
            paddedInputIds[^1] = 0;

            using var inputIdsTensor = new Tensor<int>(new TensorShape(1, paddedInputIds.Length), paddedInputIds);
            using var speedTensor = new Tensor<float>(new TensorShape(1), new[] { speed });
            using var voiceTensor = await GetVoiceVector(inputIdsTensor, voice.Tensor);

            return await Execute(inputIdsTensor, voiceTensor, speedTensor);
        }

        public async Task<Tensor<float>> Execute(Tensor<int> inputIdsTensor, Tensor<float> voiceTensor, Tensor<float> speedTensor)
        {
            LoadModelIfMissing();

            if (m_Worker == null)
            {
                Debug.LogError("[KokoroHandler] Cannot execute: Worker is null. Model likely failed to load.");
                return null;
            }

            m_Worker.Schedule(inputIdsTensor, voiceTensor, speedTensor);
            using var result = m_Worker.PeekOutput() as Tensor<float>;
            using var output = await result.ReadbackAndCloneAsync();

            var processedOutput = KokoroOutputProcessor.Apply2NotchFiltering(output);
            return processedOutput;
        }

        public static List<Voice> GetVoices()
        {
            var voices = new List<Voice>();
            var voicesList = VoicesUtils.GetVoicesList();

            foreach (var file in voicesList)
            {
                var voiceAsset = Resources.Load<RawBytesAsset>($"{k_VoicesFolderPath}{file}");

                if (voiceAsset == null)
                    continue;

                var voiceData = voiceAsset.bytes;

                var voiceArray = new float[voiceData.Length / sizeof(float)];
                Buffer.BlockCopy(voiceData, 0, voiceArray, 0, voiceData.Length);

                var styleShape = new TensorShape(voiceArray.Length / 256, 1, 256);
                var tensor = new Tensor<float>(styleShape, voiceArray);
                var voice = new Voice(file, tensor, voiceArray);
                voices.Add(voice);
            }

            return voices;
        }

        public float[] ExecuteAndExtract(int[] inputIds, float speed, Voice voice)
        {
            LoadModelIfMissing();

            if (m_Worker == null)
            {
                Debug.LogError("[KokoroHandler] Cannot execute: Worker is null. Model likely failed to load.");
                return null;
            }

            // 1. Prepare padded inputs
            var paddedInputIds = new int[inputIds.Length + 2];
            paddedInputIds[0] = 0;
            Array.Copy(inputIds, 0, paddedInputIds, 1, inputIds.Length);
            paddedInputIds[^1] = 0;

            // 2. Prepare voice style vector directly (instant, no functional graph or worker compilation!)
            int index = paddedInputIds.Length;
            int offset = index * 256;
            if (voice.RawData == null || offset + 256 > voice.RawData.Length)
            {
                offset = 0;
            }

            float[] slice = new float[256];
            if (voice.RawData != null)
            {
                Array.Copy(voice.RawData, offset, slice, 0, 256);
            }

            // 3. Create Tensors
            using var inputIdsTensor = new Tensor<int>(new TensorShape(1, paddedInputIds.Length), paddedInputIds);
            using var speedTensor = new Tensor<float>(new TensorShape(1), new[] { speed });
            using var voiceTensor = new Tensor<float>(new TensorShape(1, 256), slice);

            // 4. Run Schedule (this executes Burst compiled jobs synchronously and occupies the calling thread)
            m_Worker.Schedule(inputIdsTensor, voiceTensor, speedTensor);

            // 5. Download the output to float array
            using var result = m_Worker.PeekOutput() as Tensor<float>;
            float[] rawOutput = result.DownloadToArray();

            // 6. Apply Notch Filtering directly on the float array
            float[] processedOutput = KokoroOutputProcessor.Apply2NotchFiltering(rawOutput);

            return processedOutput;
        }

        async Task<Tensor<float>> GetVoiceVector(Tensor<int> inputIds, Tensor<float> voice)
        {
            var graph = new FunctionalGraph();
            var tokenInput = graph.AddInput<float>(voice.shape, "voice");
            var output = tokenInput[inputIds.count];
            graph.AddOutput(output, "output");
            var model = graph.Compile();

            using var worker = new Worker(model, m_BackendType);
            worker.Schedule(voice);
            using var result = worker.PeekOutput() as Tensor<float>;
            return await result.ReadbackAndCloneAsync();
        }

        public void Dispose()
        {
            m_Worker?.Dispose();
            m_Worker = null;
        }

        public class Voice: IDisposable
        {
            public string Name;
            public Tensor<float> Tensor;
            public float[] RawData;

            public Voice(string name, Tensor<float> data, float[] rawData)
            {
                Name = name;
                Tensor = data;
                RawData = rawData;
            }
            public void Dispose()
            {
                Tensor?.Dispose();
            }
        }
    }
}
