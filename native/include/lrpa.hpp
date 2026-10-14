#ifndef LRPA_HPP
#define LRPA_HPP

#include "lrpa.h"
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace lrpa {

inline std::string last_error()
{
    size_t required = 0;
    (void)lrpa_last_error(nullptr, 0, &required);
    std::vector<char> buffer(required == 0 ? 1 : required);
    if (lrpa_last_error(buffer.data(), buffer.size(), &required) != LRPA_OK)
        return "LRPA operation failed";
    return std::string(buffer.data());
}

inline void check(lrpa_result result)
{
    if (result != LRPA_OK)
        throw std::runtime_error(last_error());
}

class controller final {
public:
    explicit controller(const lrpa_options* options = nullptr)
    {
        int32_t created = 0;
        check(lrpa_open(options, &handle_, &created));
        created_ = created != 0;
    }

    ~controller() noexcept
    {
        if (handle_ != 0)
            (void)lrpa_close(handle_);
    }

    controller(const controller&) = delete;
    controller& operator=(const controller&) = delete;

    controller(controller&& other) noexcept
        : handle_(std::exchange(other.handle_, 0)), created_(other.created_) {}

    controller& operator=(controller&& other) noexcept
    {
        if (this != &other) {
            if (handle_ != 0)
                (void)lrpa_close(handle_);
            handle_ = std::exchange(other.handle_, 0);
            created_ = other.created_;
        }
        return *this;
    }

    [[nodiscard]] bool created() const noexcept { return created_; }

    void move(int32_t x, int32_t y) { check(lrpa_move(handle_, x, y)); }

    uint32_t send(const lrpa_input* inputs, uint32_t count)
    {
        uint32_t sent = 0;
        check(lrpa_send_input(handle_, count, inputs, sizeof(lrpa_input), &sent));
        return sent;
    }

    void submit(const lrpa_mouse_report& report)
    {
        check(lrpa_submit_report(handle_, &report));
    }

private:
    lrpa_handle handle_ = 0;
    bool created_ = false;
};

} // namespace lrpa

#endif
