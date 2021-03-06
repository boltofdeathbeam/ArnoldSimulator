﻿#pragma once

#include "neuron.h"

namespace GenSpecModel
{

class GenSpecInputNeuron : public Neuron
{
public:
    static const char *Type;
    GenSpecInputNeuron(NeuronBase &base, json &params);
    virtual ~GenSpecInputNeuron();

    void pup(PUP::er &p) override;

    const char *GetType() const override;

    void HandleSpike(Direction direction, MultiByteSpike &spike, Spike::Data &data) override;

    void Control(size_t brainStep) override;

    size_t ContributeToRegion(uint8_t *&contribution) override;

    void CalculateObserver(ObserverType type, std::vector<int32_t> &metadata, std::vector<uint8_t> &observerData) override;

    void SendMultiByteSpike(Direction direction, NeuronId receiver, uint8_t *values, size_t count);
protected:
    size_t mNeuronCountX;
    size_t mNeuronCountY;
    size_t mInputSizeX;
    size_t mInputSizeY;
    size_t mNeuronInputSizeX;
    size_t mNeuronInputSizeY;
    size_t mNeuronInputStrideX;
    size_t mNeuronInputStrideY;

    std::unique_ptr<uint8_t[]> mLastInput;
};

} // namespace GenSpecModel;
