Collaborators
Saint Walker, Sebastian Landeros, Jestyn Lytle

Design Doc for Web Application
Sun2Sat Connect

High Level Description
A universal scheduling tool that helps users track events and appointments in their day-to-day, and see other users that are interested. Users can make connections with each other, and connected users can see the events on each others’ schedule, making it easier to coordinate.
Instructions and Functionality
Users will have to register an account, and will be required to provide either email or phone number for event notifications(notifications can be disabled).
Users can ‘connect’ with other users, which is functionally equivalent to friending someone on another social platform. Connections(connected users) will be able to see each other's events. 
Events are added to the user itinerary via an ‘event add’ function, which will prompt users with the following options: a search for existing events; adding a general event, like working or going to the gym; building a custom event, which will require the user to input scheduling information and details about the event.
Target Audience
Students, athletes, artists, and anybody trying to meet new people but might otherwise be too busy. By advertising their schedules, users can meet each other through a shared interest.

OOP objects
User
userName [Required]
userEmail
userPhoneNumber
userEvents 
Event
eventName
eventTime
eventDescription
eventAttendees

Foreseeable Glitches
Connections still showing up when adding events, even if they cancelled the event on their side.
Events showing up from a connection that the user no longer shares.
