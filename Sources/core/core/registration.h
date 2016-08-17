#pragma once
#include <cstdint>
#include <unordered_map>
#include "log.h"

typedef uint64_t Token64;
typedef uint8_t Token8;

template<typename T, typename TToken>
class Registration
{
public:
    Registration() : mNextToken(1)
    {
        if (std::numeric_limits<TToken>::is_signed) {
            Log(LogLevel::Error, "Token type is signed (%s)", typeid(mNextToken).name());
            throw std::invalid_argument("Token type must be unsigned");
        }
    }

    TToken GetToken(const std::string &name) const
    {
        auto tokenIt = mTokens.find(name);
        if (tokenIt == mTokens.end()) {
            Log(LogLevel::Error, "Item with name %s was not registered", name);
            throw std::invalid_argument("Name not registered");
        }
        return tokenIt->second;
    }

    std::string GetName(TToken token) const
    {
        auto nameIt = mNames.find(token);
        if (nameIt == mNames.end()) {
            Log(LogLevel::Error, "Token %d does not exist", token);
            throw std::invalid_argument("Token does not exist");
        }
        return nameIt->second;
    }

    static T * GetInstance()
    {
        return &mInstance;
    }
protected:
    TToken GetNewToken(const std::string &name)
    {
        if (mTokens.find(name) != mTokens.end()) {
            Log(LogLevel::Error, "Name '%s' is already registered", name);
            throw std::invalid_argument("Name already registered");
        }

        if (mNextToken == 0) {
            Log(LogLevel::Error, "Token pool has been exhausted");
            throw std::overflow_error("Token pool has been exhausted");
        }

        TToken token = mNextToken++;
        mTokens[name] = token;
        mNames[token] = name;
        return token;
    }
private:
    TToken mNextToken;
    std::unordered_map<std::string, TToken> mTokens;
    std::unordered_map<TToken, std::string> mNames;

    static T mInstance;
};

template<typename T, typename TToken>
T Registration<T, TToken>::mInstance;