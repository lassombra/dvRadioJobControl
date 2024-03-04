# Radio Job Control
This mod enables you to manage jobs remotely with your trusty comms radio!
(testing version, expect crashes, weird behavior, might eat your cat)

Please report errors here or to the Altfuture Discord #mods-support-and-bugs

You will still need to go to the office for some paperwork, but you can combine multiple job accepts / deliveries instead of going there for every single job.

### (How to use) Select "Job Control" in your comms radio:
- pointing at cars shows their ID and job information (if any)
- selecting (clicking) a car will enable you to:
  - ACCEPT available jobs  
    A job booklet will be printed for you and put on the table if you're nearby, otherwise you need to reprint it manually once you come back to a job machine.
	
  - COMPLETE inProgress jobs  
    Provided the job conditions are fullfilled you can complete the job remotely, this will also stop the counter for your bonus time! Keep in mind the payment will only be printed once you hand in the job booklet at the station! If you don't print a booklet before you complete the job there will be no way for you to convince the machine to give you the moneys ;)
	
  - DISCARD available or inProgress jobs  
    Selfexplanatory. Might be problematic with printed job booklets / -overviews, keep in mind
	
  - REASSIGN new jobs  
    Only if PersistentJobsMod? installed. If PJ is able to generate a job it will instantly be available.
	
- Unavailable options will not be shown, e.g. if a car doesn't have a job there will be no accept / discard / etc.
- Planned features:
  - LOAD / UNLOAD  
    Will work like the normal loading terminal at the track, just with the radio (not implemented, yet)

PS: doesn't use CommsRadioAPI since this was written before that and I didn't have time for a full rewrite