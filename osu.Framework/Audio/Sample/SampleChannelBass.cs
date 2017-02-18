﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-framework/master/LICENCE

using System;
using ManagedBass;

namespace osu.Framework.Audio.Sample
{
    class SampleChannelBass : SampleChannel, IBassAudio
    {
        private volatile int channel;
        private volatile bool playing;

        bool hasChannel => channel != 0;
        bool hasSample => Sample.IsLoaded;

        float initialFrequency;

        public SampleChannelBass(Sample sample) : base(sample)
        {
        }

        void IBassAudio.UpdateDevice(int deviceIndex)
        {
            // Channels created from samples can not be migrated, so we need to ensure
            // a new channel is created after switching the device. We do not need to
            // manually free the channel, because our Bass.Free call upon switching devices
            // takes care of that.
            channel = 0;
        }

        protected override void OnStateChanged(object sender, EventArgs e)
        {
            base.OnStateChanged(sender, e);

            if (hasChannel)
            {
                Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, VolumeCalculated);
                Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, BalanceCalculated);
                Bass.ChannelSetAttribute(channel, ChannelAttribute.Frequency, initialFrequency * FrequencyCalculated);
            }
        }

        public override void Play(bool restart = true)
        {
            base.Play();

            PendingActions.Enqueue(() =>
            {
                if (!hasSample)
                {
                    channel = 0;
                    return;
                }

                // We are creating a new channel for every playback, since old channels may
                // be overridden when too many other channels are created from the same sample.
                channel = ((SampleBass)Sample).CreateChannel();
                Bass.ChannelGetAttribute(channel, ChannelAttribute.Frequency, out initialFrequency);
            });

            InvalidateState();

            PendingActions.Enqueue(() =>
            {
                if (hasChannel)
                {
                    Bass.ChannelPlay(channel, restart);
                    playing = true;
                }
            });
        }

        public override void Update()
        {
            base.Update();
            playing = hasChannel && Bass.ChannelIsActive(channel) != 0;
        }

        public override void Stop()
        {
            if (!hasChannel) return;

            base.Stop();

            PendingActions.Enqueue(() =>
            {
                Bass.ChannelStop(channel);
                // ChannelStop frees the channel.
                channel = 0;
            });
        }

        public override bool Playing => playing;
    }
}
