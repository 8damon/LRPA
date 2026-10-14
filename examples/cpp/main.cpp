#include "lrpa.hpp"
#include <iostream>

int main()
{
    try {
        lrpa::controller mouse;
        mouse.move(64, -16);
        std::cout << "created=" << std::boolalpha << mouse.created() << '\n';
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
