use lrpa::{Controller, Input};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mouse = Controller::open()?;
    let sent = mouse.send_input(&[Input::move_relative(64, -16)])?;
    println!("sent={sent} created={}", mouse.created());
    Ok(())
}
